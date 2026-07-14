using System;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
using SocialUniverse.Config;
using SocialUniverse.Economy;
using SocialUniverse.Core;
using SocialUniverse.Safety;

namespace SocialUniverse.Mining
{
    public class MiningController
    {
        private readonly IEconomyService        _economy;
        private readonly MiningRewardCalculator  _rewardCalc;
        private readonly AsteroidSpawner         _spawner;
        private readonly EconomyConfig           _config;
        private readonly PlanetDefinition        _planet;
        private readonly ActiveMiningHandoff     _handoff;
        private readonly IAudioManager           _audio;

        public DroneRuntime Drone { get; private set; }

        public IdleMiningSession CurrentIdleSession { get; private set; }
        public Asteroid          ClaimingAsteroid    { get; private set; }

        public event Action<IdleMiningSession> OnIdleSessionChanged;

        public MiningController(IEconomyService economy, MiningRewardCalculator rewardCalc,
            AsteroidSpawner spawner, EconomyConfig config, PlanetDefinition planet, ActiveMiningHandoff handoff,
            IAudioManager audio)
        {
            _economy    = economy;
            _rewardCalc = rewardCalc;
            _spawner    = spawner;
            _config     = config;
            _planet     = planet;
            _handoff    = handoff;
            _audio      = audio;
        }

        public void Initialize(DroneRuntime drone)
        {
            Drone = drone;
            TryRestoreIdleSession();
            TryFinalizePendingActiveMining();
        }

        // ---- Idle mining ----

        public bool BeginIdleMining(Asteroid asteroid)
        {
            if (asteroid == null || asteroid.IsDepleted || CurrentIdleSession != null ||
                (_handoff.AsteroidSlotId != null && _handoff.AsteroidSlotId == asteroid.SlotId))
                return false;

            var reward = _rewardCalc.Compute(asteroid);
            CurrentIdleSession = new IdleMiningSession(asteroid, DateTime.UtcNow, reward.IdleDurationSeconds);
            CurrentIdleSession.OnStageChanged += _ => OnIdleSessionChanged?.Invoke(CurrentIdleSession);

            PersistIdleSession(CurrentIdleSession);
            SULog.Info($"Idle session started on {asteroid.name} ({reward.IdleDurationSeconds:0}s)", SULog.Channel.Mining);
            OnIdleSessionChanged?.Invoke(CurrentIdleSession);
            return true;
        }

        // Player tapped the asteroid while it's ready to claim. Completes and pays out.
        public async Task ClaimIdleSessionAsync(Asteroid asteroid)
        {
            var session = CurrentIdleSession;
            if (session == null || session.Asteroid != asteroid || session.Stage != IdleMiningStage.ReadyToClaim)
                return;

            var reward = _rewardCalc.Compute(asteroid);
            session.Claim();
            _audio.PlaySfx(SfxId.MiningComplete);

            int mined = asteroid.Mine(asteroid.RemainingYield);
            if (asteroid.IsDepleted) _audio.PlaySfx(SfxId.AsteroidDestroyed);
            int coins = mined * asteroid.Definition.CoinsPerUnit;

            CurrentIdleSession = null;
            ClaimingAsteroid   = asteroid;
            ClearPersistedIdleSession();
            OnIdleSessionChanged?.Invoke(null);

            if (coins > 0)
            {
                try
                {
                    int granted = await _economy.GrantMiningRewardAsync(coins, reward.IdleDurationSeconds, reward.CoinsPerSec);
                    _audio.PlaySfx(SfxId.CoinsReward);
                    SULog.Info($"Idle session claimed: +{mined} {asteroid.Definition.MineralType} -> {granted} coins", SULog.Channel.Mining);
                }
                catch (Exception ex)
                {
                    // Asteroid is already mined-out and the session already torn down (intentional,
                    // for re-entrancy) — if the grant throws, the player loses the coins, but the
                    // asteroid must still respawn below instead of being stranded forever.
                    SULog.Error($"GrantMiningRewardAsync failed for idle claim on {asteroid.Definition.MineralType} ({coins} coins): {ex.Message}", SULog.Channel.Mining);
                }
            }

            ClaimingAsteroid = null;
            _spawner.ScheduleRespawn(asteroid, _config.AsteroidRespawnHours);
        }

        // ---- Active mining ----

        // Validates and computes the reward, then hands off to ActiveMiningHandoff — the
        // actual minigame (timer/taps) now runs entirely inside the ActiveMining scene, which
        // this MiningController instance won't exist for (it's destroyed along with Planet).
        // The caller (MiningModePromptView, via ActiveMiningRequestedEvent) is responsible for
        // triggering the FSM transition once this returns true.
        public bool BeginActiveMining(Asteroid asteroid)
        {
            if (asteroid == null || asteroid.IsDepleted) return false;
            if (CurrentIdleSession != null && CurrentIdleSession.Asteroid == asteroid) return false;
            if (_handoff.AsteroidSlotId != null) return false; // a previous result is still pending finalize

            var reward = _rewardCalc.Compute(asteroid);
            _handoff.Begin(_planet.PlanetId, asteroid.SlotId, asteroid.Definition, asteroid.RemainingYield,
                reward.ActiveTapsRequired, _config.ActiveMaxErrors, reward.ActiveSessionDurationSeconds);
            return true;
        }

        // Called from Initialize (same spot idle-session restore already runs) once Planet has
        // reloaded after an active-mining round trip. Resolves the asteroid back by SlotId
        // (same tolerance TryRestoreIdleSession already has: if the slot no longer resolves,
        // silently drop it rather than throwing) and finishes the grant/respawn flow.
        // Clears the handoff unconditionally whenever one is pending, even if it never got a
        // result — reaching Planet with a populated-but-unresolved handoff means the session
        // was abandoned (only reachable via the standalone dev workflow, where nothing consumes
        // ActiveMiningRequestedEvent), and leaving it set would otherwise permanently block
        // future active/idle mining on that asteroid.
        private void TryFinalizePendingActiveMining()
        {
            if (_handoff.AsteroidSlotId == null) return;

            if (_handoff.HasResult)
            {
                var asteroid = _spawner.FindBySlotId(_handoff.AsteroidSlotId);
                if (asteroid != null)
                {
                    if (_handoff.Succeeded) _ = CompleteActiveMiningAsync(asteroid);
                    else                     FailActiveMining(asteroid);
                }
            }

            _handoff.Clear();
        }

        private async Task CompleteActiveMiningAsync(Asteroid asteroid)
        {
            var reward = _rewardCalc.Compute(asteroid);

            int mined = asteroid.Mine(asteroid.RemainingYield);
            int coins = mined * asteroid.Definition.CoinsPerUnit;

            if (coins > 0)
            {
                try
                {
                    int granted = await _economy.GrantMiningRewardAsync(coins, reward.IdleDurationSeconds, reward.CoinsPerSec);
                    SULog.Info($"Active mining success: +{mined} {asteroid.Definition.MineralType} -> {granted} coins", SULog.Channel.Mining);
                }
                catch (Exception ex)
                {
                    // Same reasoning as ClaimIdleSessionAsync: the asteroid is already mined-out
                    // (intentional, for re-entrancy) — if the grant throws, the player loses the
                    // coins, but the asteroid must still respawn below instead of being stranded.
                    SULog.Error($"GrantMiningRewardAsync failed for active-mining success on {asteroid.Definition.MineralType} ({coins} coins): {ex.Message}", SULog.Channel.Mining);
                }
            }

            _spawner.ScheduleRespawn(asteroid, _config.AsteroidRespawnHours);
        }

        private void FailActiveMining(Asteroid asteroid)
        {
            asteroid.Mine(asteroid.RemainingYield);

            SULog.Info($"Active mining failed on {asteroid.name} — asteroid lost", SULog.Channel.Mining);
            _spawner.ScheduleRespawn(asteroid, _config.AsteroidRespawnHours);
        }

        // ---- Idle session persistence (survives the app being closed) ----

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

            CurrentIdleSession = new IdleMiningSession(asteroid, startUtc, duration, restored: true);
            CurrentIdleSession.OnStageChanged += _ => OnIdleSessionChanged?.Invoke(CurrentIdleSession);

            SULog.Info($"Idle session restored on {asteroid.name} (stage={CurrentIdleSession.Stage})", SULog.Channel.Mining);
            OnIdleSessionChanged?.Invoke(CurrentIdleSession);
        }

        private void PersistIdleSession(IdleMiningSession session)
        {
            string duration = session.DurationSeconds.ToString(CultureInfo.InvariantCulture);
            string value    = $"{_planet.PlanetId}|{session.Asteroid.SlotId}|{session.StartUtc:O}|{duration}";
            PlayerPrefs.SetString(SaveKeys.IdleMiningSession, value);
            PlayerPrefs.Save();
        }

        private static void ClearPersistedIdleSession()
        {
            PlayerPrefs.DeleteKey(SaveKeys.IdleMiningSession);
            PlayerPrefs.Save();
        }
    }
}
