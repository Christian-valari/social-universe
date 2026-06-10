using System;
using System.Threading.Tasks;
using UnityEngine;
using SocialUniverse.Config;
using SocialUniverse.Economy;
using SocialUniverse.Core;

namespace SocialUniverse.Mining
{
    public enum MiningPhase { Idle, Active, ReturningCargo }

    public class MiningController
    {
        private readonly IEconomyService      _economy;
        private readonly IdleMiningCalculator _idleCalc;
        private readonly ActiveMiningMinigame _minigame;
        private readonly AsteroidSpawner      _spawner;
        private readonly EconomyConfig        _config;

        public MiningPhase Phase         { get; private set; }
        public DroneRuntime Drone        { get; private set; }
        public Asteroid     CurrentTarget { get; private set; }

        public IdleMiningSession CurrentIdleSession { get; private set; }
        public Asteroid          ClaimingAsteroid   { get; private set; }

        public event Action<MiningPhase> OnPhaseChanged;
        public event Action<IdleMiningSession> OnIdleSessionChanged;

        public MiningController(IEconomyService economy, IdleMiningCalculator idleCalc,
            ActiveMiningMinigame minigame, AsteroidSpawner spawner, EconomyConfig config)
        {
            _economy  = economy;
            _idleCalc = idleCalc;
            _minigame = minigame;
            _spawner  = spawner;
            _config   = config;
        }

        public void StartSession(DroneRuntime drone, DateTime lastSessionEnd)
        {
            Drone = drone;

            int offlineYield = _idleCalc.Calculate(lastSessionEnd, drone);
            if (offlineYield > 0)
            {
                drone.AddCargo(offlineYield);
                SULog.Info($"Mining: offline yield = {offlineYield} units", SULog.Channel.Mining);
                _ = CommitCargoAsync();
                return;
            }

            PickNextTarget();
            SetPhase(MiningPhase.Active);
        }

        public MiningTapResult Tap()
        {
            if (CurrentIdleSession != null) return null; // drone is busy on a player-directed session

            var result = _minigame.Tap(CurrentTarget, Drone);
            if (CurrentTarget != null && CurrentTarget.IsDepleted)
                PickNextTarget();
            return result;
        }

        // Player chose "Idle Mine" on an asteroid — claims the drone for a timed session.
        public bool BeginIdleMining(Asteroid asteroid)
        {
            if (asteroid == null || asteroid.IsDepleted || CurrentIdleSession != null)
                return false;

            CurrentIdleSession = new IdleMiningSession(asteroid, _config.IdleSessionDuration, _config.IdleSessionClaimTaps);
            SULog.Info($"Idle session started on {asteroid.name}", SULog.Channel.Mining);
            OnIdleSessionChanged?.Invoke(CurrentIdleSession);
            return true;
        }

        public void NotifyIdleSessionStageChanged() => OnIdleSessionChanged?.Invoke(CurrentIdleSession);

        // Player tapped the asteroid while it's ready to claim. Completes and pays out on the final tap.
        public async Task RegisterIdleClaimTapAsync(Asteroid asteroid)
        {
            var session = CurrentIdleSession;
            if (session == null || session.Asteroid != asteroid) return;

            bool complete = session.RegisterClaimTap();
            OnIdleSessionChanged?.Invoke(session);
            if (!complete) return;

            int targetYield = Mathf.RoundToInt(_config.IdleMiningRate * _config.IdleSessionDuration);
            int mined       = asteroid.Mine(targetYield);
            int coins       = mined * asteroid.Definition.CoinsPerUnit;

            CurrentIdleSession = null;
            ClaimingAsteroid   = asteroid;
            OnIdleSessionChanged?.Invoke(null);

            if (coins > 0)
            {
                await _economy.GrantCoinsAsync(coins);
                SULog.Info($"Idle session claimed: +{mined} {asteroid.Definition.MineralType} -> {coins} coins", SULog.Channel.Mining);
            }

            ClaimingAsteroid = null;
            bool wasCurrentTarget = CurrentTarget == asteroid;
            _spawner.ScheduleRespawn(asteroid, _config.AsteroidRespawnHours);

            if (wasCurrentTarget)
                PickNextTarget();
        }

        public async Task CommitCargoAsync()
        {
            SetPhase(MiningPhase.ReturningCargo);
            int hauled = Drone.EmptyCargo();

            if (hauled > 0)
            {
                int coins = hauled * (CurrentTarget?.Definition.CoinsPerUnit ?? 1);
                await _economy.GrantCoinsAsync(coins);
                SULog.Info($"Mining: committed {hauled} units → {coins} coins", SULog.Channel.Mining);
            }

            PickNextTarget();
            SetPhase(MiningPhase.Active);
        }

        private void PickNextTarget()
        {
            CurrentTarget = null;
            foreach (var a in _spawner.ActiveAsteroids)
            {
                if (!a.IsDepleted) { CurrentTarget = a; break; }
            }
        }

        private void SetPhase(MiningPhase phase)
        {
            Phase = phase;
            OnPhaseChanged?.Invoke(phase);
        }
    }
}
