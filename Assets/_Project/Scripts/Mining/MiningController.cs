using System;
using System.Threading.Tasks;
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

        public MiningPhase Phase         { get; private set; }
        public DroneRuntime Drone        { get; private set; }
        public Asteroid     CurrentTarget { get; private set; }

        public event Action<MiningPhase> OnPhaseChanged;

        public MiningController(IEconomyService economy, IdleMiningCalculator idleCalc,
            ActiveMiningMinigame minigame, AsteroidSpawner spawner)
        {
            _economy  = economy;
            _idleCalc = idleCalc;
            _minigame = minigame;
            _spawner  = spawner;
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

        public MiningTapResult Tap() => _minigame.Tap(CurrentTarget, Drone);

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
