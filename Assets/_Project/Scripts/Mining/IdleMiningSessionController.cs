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

        private IdleMiningSession _trackedSession;

        public IdleMiningSessionController(MiningController mining, DroneController drone)
        {
            _mining = mining;
            _drone  = drone;
        }

        public void Start() => EventBus.Subscribe<AsteroidSelectedEvent>(OnAsteroidSelected);

        public void Dispose() => EventBus.Unsubscribe<AsteroidSelectedEvent>(OnAsteroidSelected);

        public void Tick()
        {
            var session = _mining.CurrentIdleSession;

            if (session != _trackedSession)
            {
                _trackedSession = session;
                if (session != null)
                    _drone.SetTarget(session.Asteroid.transform);
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
    }
}
