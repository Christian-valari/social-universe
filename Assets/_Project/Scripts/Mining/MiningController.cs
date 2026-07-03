using System;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
using SocialUniverse.Config;
using SocialUniverse.Economy;
using SocialUniverse.Core;

namespace SocialUniverse.Mining
{
    public class MiningController
    {
        private readonly IEconomyService        _economy;
        private readonly MiningRewardCalculator  _rewardCalc;
        private readonly ActiveMiningMinigame    _activeMinigame;
        private readonly AsteroidSpawner         _spawner;
        private readonly EconomyConfig           _config;
        private readonly PlanetDefinition        _planet;

        public DroneRuntime Drone { get; private set; }

        public IdleMiningSession   CurrentIdleSession   { get; private set; }
        public ActiveMiningSession CurrentActiveSession => _activeMinigame.CurrentSession;
        public Asteroid            ClaimingAsteroid     { get; private set; }

        public event Action<IdleMiningSession>   OnIdleSessionChanged;
        public event Action<ActiveMiningSession> OnActiveSessionChanged;

        public MiningController(IEconomyService economy, MiningRewardCalculator rewardCalc,
            ActiveMiningMinigame activeMinigame, AsteroidSpawner spawner, EconomyConfig config, PlanetDefinition planet)
        {
            _economy        = economy;
            _rewardCalc     = rewardCalc;
            _activeMinigame = activeMinigame;
            _spawner        = spawner;
            _config         = config;
            _planet         = planet;

            _activeMinigame.OnSessionChanged += OnActiveMinigameSessionChanged;
        }

        public void Initialize(DroneRuntime drone)
        {
            Drone = drone;
            TryRestoreIdleSession();
        }

        // ---- Idle mining ----

        public bool BeginIdleMining(Asteroid asteroid)
        {
            if (asteroid == null || asteroid.IsDepleted || CurrentIdleSession != null)
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

            int mined = asteroid.Mine(asteroid.RemainingYield);
            int coins = mined * asteroid.Definition.CoinsPerUnit;

            CurrentIdleSession = null;
            ClaimingAsteroid   = asteroid;
            ClearPersistedIdleSession();
            OnIdleSessionChanged?.Invoke(null);

            if (coins > 0)
            {
                int granted = await _economy.GrantMiningRewardAsync(coins, reward.IdleDurationSeconds, reward.CoinsPerSec);
                SULog.Info($"Idle session claimed: +{mined} {asteroid.Definition.MineralType} -> {granted} coins", SULog.Channel.Mining);
            }

            ClaimingAsteroid = null;
            _spawner.ScheduleRespawn(asteroid, _config.AsteroidRespawnHours);
        }

        // ---- Active mining ----

        public bool BeginActiveMining(Asteroid asteroid) => _activeMinigame.Begin(asteroid);

        public void TickActiveSession(float deltaTime) => _activeMinigame.Tick(deltaTime);

        public void RegisterActiveTap(bool hitTarget) => _activeMinigame.RegisterTap(hitTarget);

        private void OnActiveMinigameSessionChanged(ActiveMiningSession session)
        {
            OnActiveSessionChanged?.Invoke(session);

            if (session == null) return;
            if (session.Stage == ActiveMiningStage.Success) _ = CompleteActiveMiningAsync(session);
            else if (session.Stage == ActiveMiningStage.Failed) FailActiveMining(session);
        }

        private async Task CompleteActiveMiningAsync(ActiveMiningSession session)
        {
            var asteroid = session.Asteroid;
            var reward   = _rewardCalc.Compute(asteroid);

            int mined = asteroid.Mine(asteroid.RemainingYield);
            int coins = mined * asteroid.Definition.CoinsPerUnit;

            _activeMinigame.Clear();
            OnActiveSessionChanged?.Invoke(null);

            if (coins > 0)
            {
                int granted = await _economy.GrantMiningRewardAsync(coins, reward.IdleDurationSeconds, reward.CoinsPerSec);
                SULog.Info($"Active mining success: +{mined} {asteroid.Definition.MineralType} -> {granted} coins", SULog.Channel.Mining);
            }

            _spawner.ScheduleRespawn(asteroid, _config.AsteroidRespawnHours);
        }

        private void FailActiveMining(ActiveMiningSession session)
        {
            var asteroid = session.Asteroid;
            asteroid.Mine(asteroid.RemainingYield);

            _activeMinigame.Clear();
            OnActiveSessionChanged?.Invoke(null);

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

            CurrentIdleSession = new IdleMiningSession(asteroid, startUtc, duration);
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
