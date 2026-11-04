using System;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Safety;

namespace SocialUniverse.Mining
{
    public class MiningController
    {
        private readonly IMineralService         _minerals;
        private readonly MiningRewardCalculator  _rewardCalc;
        private readonly AsteroidSpawner         _spawner;
        private readonly EconomyConfig           _config;
        private readonly PlanetDefinition        _planet;
        private readonly ActiveMiningHandoff     _handoff;
        private readonly IAudioManager           _audio;
        private readonly DroneFleet              _fleet;
        private readonly IIdleMiningService      _idle;
        private readonly ActiveMiningRunStore    _runStore;

        public IdleMiningSession CurrentIdleSession { get; private set; }
        public Asteroid          ClaimingAsteroid    { get; private set; }
        // The asteroid whose StartIdleMining call is in flight (the drone is committed to it).
        public Asteroid          StartingAsteroid    { get; private set; }
        // What an idle run pays, as a share of a perfect active run (the asteroid tap prompt shows it).
        public float             IdleYieldFraction   => _config.IdleYieldFraction;

        public event Action<IdleMiningSession> OnIdleSessionChanged;

        public MiningController(IMineralService minerals, MiningRewardCalculator rewardCalc,
            AsteroidSpawner spawner, EconomyConfig config, PlanetDefinition planet, ActiveMiningHandoff handoff,
            IAudioManager audio, DroneFleet fleet, IIdleMiningService idle, ActiveMiningRunStore runStore)
        {
            _minerals   = minerals;
            _rewardCalc = rewardCalc;
            _spawner    = spawner;
            _config     = config;
            _planet     = planet;
            _handoff    = handoff;
            _audio      = audio;
            _fleet      = fleet;
            _idle       = idle;
            _runStore   = runStore;
        }

        // serverRun: the player's idle run as GetIdleMining reported it (null if the read failed).
        // A pending active-mining result is finalized first: it may have consumed the asteroid the
        // idle run is on (possible after a failed GetIdleMining), which EnsureSlot then brings back.
        // The same goes for a run left unsettled, which is only looked at when no result was pending:
        // a normal claim that just failed leaves its marker for the next load to retry, and looking at
        // it on this load would clear it, since its asteroid is already used up.
        public void Initialize(IdleMiningSessionResult serverRun = null)
        {
            if (!TryFinalizePendingActiveMining())
                TrySettleLeftoverRun();

            if (!TryRestoreServerRun(serverRun))
                TryRestoreIdleSession();
        }

        // ---- Idle mining ----
        // A run lives on the server (StartIdleMining), so any device the player signs in on can show
        // and claim it: docs/superpowers/plans/2026-10-15-server-tracked-idle-mining.md.

        // Starts an idle run on the asteroid once the server has recorded it. False if the run can't
        // start here (a refusal is published as MiningClaimRejectedEvent), including when the server
        // already has a run on this planet, which is then taken over instead.
        public async Task<bool> BeginIdleMiningAsync(Asteroid asteroid)
        {
            // While a claim is in flight the server still holds the run being claimed (ValidateMining
            // ends it before answering), so a start now would collide with it.
            if (asteroid == null || asteroid.IsDepleted || CurrentIdleSession != null || StartingAsteroid != null ||
                ClaimingAsteroid != null ||
                (_handoff.AsteroidSlotId != null && _handoff.AsteroidSlotId == asteroid.SlotId))
                return false;

            var active = _fleet.Active;
            if (active == null || active.Definition.Tier < asteroid.Definition.Tier)
            {
                EventBus.Publish(new MiningBlockedEvent { Asteroid = asteroid, RequiredTier = asteroid.Definition.Tier });
                return false;
            }

            var reward    = _rewardCalc.Compute(asteroid, active.EffectiveYieldMult, active.EffectiveTravelSpeed);
            var mineralId = MineralIdOf(asteroid);

            // The drone is committed until the server answers: no second start, no active mining.
            StartingAsteroid = asteroid;
            IdleMiningSessionResult res;
            try
            {
                res = await _idle.StartAsync(_planet.PlanetId, asteroid.SlotId, mineralId, reward.IdleDurationSeconds);
                if (asteroid == null) return false; // the Planet scene went away while the server answered

                if (res.Reason == "ALREADY_MINING" && res.HasSession && res.PlanetId == _planet.PlanetId)
                {
                    if (AdoptServerRun(res, restored: false))
                    {
                        SULog.Info($"Idle start on {asteroid.SlotId} refused: the server's run on {res.SlotId} is taken over", SULog.Channel.Mining);
                        return false;
                    }

                    // That run's asteroid can't be on this planet (e.g. its type was removed), so it
                    // would block idle mining here for good: replace it with this run.
                    SULog.Warn($"Idle run on {res.SlotId} can't be placed on {_planet.PlanetId}; replacing it", SULog.Channel.Mining);
                    res = await _idle.StartAsync(_planet.PlanetId, asteroid.SlotId, mineralId, reward.IdleDurationSeconds, res.SlotId);
                    if (asteroid == null) return false;
                }
            }
            finally
            {
                StartingAsteroid = null;
            }

            if (res.Success)
            {
                float duration = res.DurationSeconds > 0f ? res.DurationSeconds : reward.IdleDurationSeconds;
                SetSession(new IdleMiningSession(asteroid, StartUtcFor(res), duration, serverTracked: true));
                SULog.Info($"Idle session started on {asteroid.SlotId} ({duration:0}s)", SULog.Channel.Mining);
                return true;
            }

            SULog.Warn($"Idle start on {asteroid.SlotId} refused: {res.Reason}", SULog.Channel.Mining);
            EventBus.Publish(new MiningClaimRejectedEvent { MineralId = mineralId, Reason = res.Reason });
            return false;
        }

        // Player tapped the asteroid while it's ready to claim. Completes and pays out.
        public async Task ClaimIdleSessionAsync(Asteroid asteroid)
        {
            var session = CurrentIdleSession;
            if (session == null || session.Asteroid != asteroid || session.Stage != IdleMiningStage.ReadyToClaim)
                return;

            var reward = _rewardCalc.Compute(asteroid, _fleet.Active.EffectiveYieldMult, _fleet.Active.EffectiveTravelSpeed);
            session.Claim();
            _audio.PlaySfx(SfxId.MiningComplete);

            int mined = asteroid.Mine(asteroid.RemainingYield);
            if (asteroid.IsDepleted) _audio.PlaySfx(SfxId.AsteroidDestroyed);
            int quantity  = reward.IdleQuantity;
            var mineral   = asteroid.Definition.Mineral;
            var mineralId = MineralIdOf(asteroid);

            // A server-tracked run is ended by this claim, so it must reach the server even when it is
            // worth nothing (ValidateMining then answers INVALID_PARAMS and still ends the run); a
            // legacy local session has no server run to end.
            string idleSlotId = session.IsServerTracked ? asteroid.SlotId : null;

            CurrentIdleSession = null;
            ClaimingAsteroid   = asteroid;
            ClearPersistedIdleSession();
            OnIdleSessionChanged?.Invoke(null);

            if ((quantity > 0 && mineral != null) || idleSlotId != null)
            {
                try
                {
                    var result = await _minerals.GrantMiningAsync(_planet.PlanetId, mineralId, quantity, idleSlotId, asteroid.SlotId);
                    if (result.Granted > 0)
                    {
                        _audio.PlaySfx(SfxId.CoinsReward);
                        EventBus.Publish(new IdleClaimCompletedEvent { MineralId = mineralId, Quantity = result.Granted });
                        SULog.Info($"Idle session claimed: +{result.Granted} {mineralId}", SULog.Channel.Mining);
                    }
                    else
                    {
                        SULog.Warn($"Idle claim rejected for {mineralId}: {result.Reason}", SULog.Channel.Mining);
                        EventBus.Publish(new MiningClaimRejectedEvent { MineralId = mineralId, Reason = result.Reason });
                    }
                }
                catch (Exception ex)
                {
                    // The asteroid is already mined-out and the session torn down (intentional, for
                    // re-entrancy). If the grant throws, a legacy session's minerals are lost, while a
                    // server-tracked run stays on the server and comes back ready on the next load.
                    // Either way the asteroid must still respawn below instead of being stranded.
                    SULog.Error($"GrantMiningAsync failed for idle claim on {mineralId} ({quantity}): {ex.Message}", SULog.Channel.Mining);
                }
            }

            ClaimingAsteroid = null;
            _spawner.ScheduleRespawn(asteroid, _config.AsteroidRespawnHours);
        }

        // ---- Active mining ----

        // Validates and computes the reward, then hands off to ActiveMiningHandoff, pinning the full
        // quantity and the tier's settings — the actual minigame now runs entirely inside the
        // ActiveMining scene, which this MiningController instance won't exist for (it's destroyed
        // along with Planet).
        // The caller (MiningModePromptView, via ActiveMiningRequestedEvent) is responsible for
        // triggering the FSM transition once this returns true.
        public bool BeginActiveMining(Asteroid asteroid)
        {
            if (asteroid == null || asteroid.IsDepleted) return false;
            if (CurrentIdleSession != null) return false; // the drone is out on an idle session (on any asteroid)
            if (StartingAsteroid != null) return false;  // ...or about to leave on one
            if (_handoff.AsteroidSlotId != null) return false; // a previous result is still pending finalize

            var active = _fleet.Active;
            if (active == null || active.Definition.Tier < asteroid.Definition.Tier)
            {
                EventBus.Publish(new MiningBlockedEvent { Asteroid = asteroid, RequiredTier = asteroid.Definition.Tier });
                return false;
            }

            var reward = _rewardCalc.Compute(asteroid, _fleet.Active.EffectiveYieldMult, _fleet.Active.EffectiveTravelSpeed);
            _handoff.Begin(_planet.PlanetId, asteroid.SlotId, asteroid.Definition, reward.MineralQuantity,
                _config.ActiveRunSettings(asteroid.Definition.Tier));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _handoff.DevSettingsForTier = _config.ActiveRunSettings;
#endif
            return true;
        }

        // Called from Initialize (same spot idle-session restore already runs) once Planet has
        // reloaded after an active-mining round trip. Resolves the asteroid back by SlotId
        // (same tolerance TryRestoreIdleSession already has: if the slot no longer resolves,
        // silently drop it, and the run's marker, rather than throwing) and finishes the
        // grant/respawn flow. Returns whether there was a result to handle.
        // Clears the handoff unconditionally whenever one is pending, even if it never got a
        // result — reaching Planet with a populated-but-unresolved handoff means the session
        // was abandoned (only reachable via the standalone dev workflow, where nothing consumes
        // ActiveMiningRequestedEvent), and leaving it set would otherwise permanently block
        // future active/idle mining on that asteroid.
        private bool TryFinalizePendingActiveMining()
        {
            if (_handoff.AsteroidSlotId == null) return false;

            bool hadResult = _handoff.HasResult;
            if (hadResult)
            {
                var asteroid = _spawner.FindBySlotId(_handoff.AsteroidSlotId);
                if (asteroid != null) SettleActiveMining(asteroid, _handoff.BankedQuantity);
                else                  _runStore.Clear();
            }

            _handoff.Clear();
            return hadResult;
        }

        // A marker with no result behind it: the app closed mid-run or on the result screen, or the
        // last claim got no answer. It settles like a failed run, paying what was banked and using up
        // the asteroid. A slot that's gone means the claim already landed (the server's claim log keeps
        // it off the field), so there is nothing left to do.
        private void TrySettleLeftoverRun()
        {
            var record = _runStore.Load();
            if (record == null) return;

            var asteroid = record.PlanetId == _planet.PlanetId ? _spawner.FindBySlotId(record.SlotId) : null;
            if (asteroid == null || asteroid.IsDepleted)
            {
                _runStore.Clear();
                return;
            }

            int banked = ActiveMiningPayout.Quantity(record.FullQuantity, record.BankedFraction);
            SULog.Info($"Settling the unfinished run on {record.SlotId}: {banked}/{record.FullQuantity} {record.MineralId}", SULog.Channel.Mining);
            SettleActiveMining(asteroid, banked);
        }

        private void SettleActiveMining(Asteroid asteroid, int banked)
        {
            if (banked > 0) _ = CompleteActiveMiningAsync(asteroid, banked);
            else            FailActiveMining(asteroid);
        }

        // quantity is the share the run banked of the full amount pinned when it began: Planet's reload
        // re-rolls asteroid yields, so the reloaded asteroid no longer says what the run was worth.
        // This run's marker is cleared once the server answers, grant or rejection. A call that throws
        // got no answer, so the marker stays and the next Planet load retries it.
        private async Task CompleteActiveMiningAsync(Asteroid asteroid, int quantity)
        {
            string slotId = asteroid.SlotId;
            int mined = asteroid.Mine(asteroid.RemainingYield);
            var mineral = asteroid.Definition.Mineral;

            if (quantity > 0 && mineral != null)
            {
                try
                {
                    var result = await _minerals.GrantMiningAsync(_planet.PlanetId, mineral.MineralId, quantity, slotId: slotId);
                    _runStore.Clear(_planet.PlanetId, slotId);
                    if (result.Granted > 0)
                    {
                        SULog.Info($"Active mining success: +{result.Granted} {mineral.MineralId}", SULog.Channel.Mining);
                    }
                    else
                    {
                        SULog.Warn($"Active mining claim rejected for {mineral.MineralId}: {result.Reason}", SULog.Channel.Mining);
                        EventBus.Publish(new MiningClaimRejectedEvent { MineralId = mineral.MineralId, Reason = result.Reason });
                    }
                }
                catch (Exception ex)
                {
                    // Same reasoning as ClaimIdleSessionAsync: the asteroid is already mined-out
                    // (intentional, for re-entrancy) — if the grant throws, the marker keeps the
                    // claim for the next Planet load, and the asteroid must still respawn below
                    // instead of being stranded.
                    SULog.Error($"GrantMiningAsync failed for active-mining success on {mineral.MineralId} ({quantity}): {ex.Message}", SULog.Channel.Mining);
                }
            }
            else
            {
                _runStore.Clear(_planet.PlanetId, slotId); // nothing can be claimed, so there is nothing to retry
            }

            _spawner.ScheduleRespawn(asteroid, _config.AsteroidRespawnHours);
        }

        // A failed minigame loses the asteroid. The slot empties here at once, and the server records it
        // (ForfeitMiningAsync: it uses one of the planet's claims and grants nothing) so it stays empty on
        // every device until it respawns.
        private void FailActiveMining(Asteroid asteroid)
        {
            string slotId    = asteroid.SlotId;
            string mineralId = MineralIdOf(asteroid);
            asteroid.Mine(asteroid.RemainingYield);

            SULog.Info($"Active mining failed on {asteroid.name} — asteroid lost", SULog.Channel.Mining);
            _spawner.ScheduleRespawn(asteroid, _config.AsteroidRespawnHours);
            _ = ForfeitAsync(mineralId, slotId);
        }

        // Never throws. The player already saw the run fail, so a forfeit the server doesn't record (an
        // old server, a full claim budget) is only logged: the slot is then empty on this device until
        // the next Planet load. This run's marker is cleared once the server answers; with no answer (no
        // connection) it stays, and the next Planet load forfeits again.
        private async Task ForfeitAsync(string mineralId, string slotId)
        {
            try
            {
                var result = await _minerals.ForfeitMiningAsync(_planet.PlanetId, mineralId, slotId);
                _runStore.Clear(_planet.PlanetId, slotId);
                if (result.Reason != "FORFEITED")
                    SULog.Warn($"Forfeit of {slotId} not recorded: {result.Reason}", SULog.Channel.Mining);
            }
            catch (Exception ex)
            {
                SULog.Warn($"Forfeit of {slotId} failed ({ex.Message}); it is empty on this device only", SULog.Channel.Mining);
            }
        }

        // ---- Idle session restore ----

        // The server's run on this planet (from any device) wins over anything this device kept.
        // Returns false when there is none here, so Initialize falls back to a legacy session.
        private bool TryRestoreServerRun(IdleMiningSessionResult run)
        {
            if (run == null || !run.HasSession || run.PlanetId != _planet.PlanetId) return false;

            ClearPersistedIdleSession();
            if (!AdoptServerRun(run, restored: true))
                SULog.Warn($"Idle run on {run.SlotId} can't be placed on {_planet.PlanetId}; not shown", SULog.Channel.Mining);
            return true;
        }

        // Attaches a server run to its asteroid (spawning the slot if this device has it waiting to
        // respawn). False if the slot can't be on this planet.
        private bool AdoptServerRun(IdleMiningSessionResult run, bool restored)
        {
            var asteroid = _spawner.EnsureSlot(run.SlotId);
            if (asteroid == null || asteroid.IsDepleted) return false;

            SetSession(new IdleMiningSession(asteroid, StartUtcFor(run), run.DurationSeconds, restored, serverTracked: true));
            SULog.Info($"Idle session on {run.SlotId} taken from the server ({run.ElapsedSeconds:0}/{run.DurationSeconds:0}s, stage={CurrentIdleSession.Stage})", SULog.Channel.Mining);
            return true;
        }

        // The run's start on this device's clock: the server reports how long ago it started on its
        // own clock, so a device whose clock is off still shows the right progress.
        private static DateTime StartUtcFor(IdleMiningSessionResult run) =>
            DateTime.UtcNow - TimeSpan.FromSeconds(Math.Max(0f, run.ElapsedSeconds));

        private void SetSession(IdleMiningSession session)
        {
            CurrentIdleSession = session;
            session.OnStageChanged += _ => OnIdleSessionChanged?.Invoke(CurrentIdleSession);
            OnIdleSessionChanged?.Invoke(CurrentIdleSession);
        }

        private static string MineralIdOf(Asteroid asteroid) =>
            asteroid.Definition.Mineral != null ? asteroid.Definition.Mineral.MineralId : asteroid.Definition.MineralType;

        // Legacy: builds before server-tracked runs kept the session only in PlayerPrefs. Such a
        // session is restored on this device as before and claimed without a server run; new builds
        // never write the key, only read and delete it.
        private void TryRestoreIdleSession()
        {
            var raw = PlayerPrefs.GetString(SaveKeys.IdleMiningSession, "");
            if (string.IsNullOrEmpty(raw)) return;

            var parts = raw.Split('|');
            if (parts.Length != 4)
            {
                ClearPersistedIdleSession();
                return;
            }

            string planetId = parts[0];
            string slotId   = parts[1];

            bool validTimestamp = DateTime.TryParse(parts[2], null, DateTimeStyles.RoundtripKind, out var startUtc);
            bool validDuration  = float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var duration);

            if (planetId != _planet.PlanetId || !validTimestamp || !validDuration)
            {
                ClearPersistedIdleSession();
                return;
            }

            var asteroid = _spawner.FindBySlotId(slotId);
            if (asteroid == null || asteroid.IsDepleted)
            {
                ClearPersistedIdleSession();
                return;
            }

            SetSession(new IdleMiningSession(asteroid, startUtc, duration, restored: true));
            SULog.Info($"Legacy idle session restored on {asteroid.name} (stage={CurrentIdleSession.Stage})", SULog.Channel.Mining);
        }

        private static void ClearPersistedIdleSession()
        {
            PlayerPrefs.DeleteKey(SaveKeys.IdleMiningSession);
            PlayerPrefs.Save();
        }
    }
}
