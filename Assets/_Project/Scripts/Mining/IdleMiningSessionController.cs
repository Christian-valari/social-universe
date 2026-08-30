using System;
using UnityEngine;
using VContainer.Unity;
using SocialUniverse.Core;

namespace SocialUniverse.Mining
{
    // Drives a player-directed idle mining session end to end:
    // travel to the asteroid -> wall-clock mining wait -> single-tap claim.
    public class IdleMiningSessionController : ITickable, IStartable, IDisposable
    {
        private readonly MiningController _mining;
        private readonly DroneController  _drone;
        private readonly DroneFleet       _fleet;

        private IdleMiningSession _trackedSession;

        public IdleMiningSessionController(MiningController mining, DroneController drone, DroneFleet fleet)
        {
            _mining = mining;
            _drone  = drone;
            _fleet  = fleet;
        }

        public void Start()
        {
            EventBus.Subscribe<AsteroidSelectedEvent>(OnAsteroidSelected);
            EventBus.Subscribe<DroneFleetChangedEvent>(OnFleetChanged);
            RefreshRestingModel(); // show the active drone at base as soon as the fleet is known
        }

        public void Dispose()
        {
            EventBus.Unsubscribe<AsteroidSelectedEvent>(OnAsteroidSelected);
            EventBus.Unsubscribe<DroneFleetChangedEvent>(OnFleetChanged);
        }

        public void Tick()
        {
            var session = _mining.CurrentIdleSession;

            if (session != _trackedSession)
            {
                _trackedSession = session;
                if (session != null)
                {
                    // The drone that goes mining must be the one the player has selected.
                    _drone.SetModel(ActiveModelPrefab());
                    if (session.WasRestored)
                        _drone.SnapToTarget(session.Asteroid.transform);
                    else
                        _drone.SetTarget(session.Asteroid.transform);
                }
                else
                    _drone.ReturnToBase(); // asteroid claimed — head back to base
            }

            if (session == null) return;

            session.Tick(Time.deltaTime);

            if (session.Stage == IdleMiningStage.Traveling && _drone.IsAtTarget)
                session.BeginMining();
        }

        private void OnAsteroidSelected(AsteroidSelectedEvent e)
        {
            var session = _mining.CurrentIdleSession;
            if (session != null && session.Asteroid == e.Asteroid && session.Stage == IdleMiningStage.ReadyToClaim)
                _ = _mining.ClaimIdleSessionAsync(e.Asteroid);
        }

        // Fleet loaded or selection changed: refresh the drone resting at base (but never yank the
        // model out from under an in-flight mining session).
        private void OnFleetChanged(DroneFleetChangedEvent _)
        {
            if (_mining.CurrentIdleSession == null) RefreshRestingModel();
        }

        private void RefreshRestingModel() => _drone.SetModel(ActiveModelPrefab());

        private GameObject ActiveModelPrefab() => _fleet.Active?.Definition?.ModelPrefab;
    }
}
