using System;
using UnityEngine;
using VContainer.Unity;
using SocialUniverse.Core;

namespace SocialUniverse.Mining
{
    // Drives a player-directed idle mining session end to end:
    // travel to the asteroid -> timed mining (with VFX) -> claim taps -> payout.
    public class IdleMiningSessionController : ITickable, IStartable, IDisposable
    {
        private readonly MiningController _mining;
        private readonly DroneController  _drone;

        private IdleMiningSession _trackedSession;
        private ParticleSystem    _vfx;

        public IdleMiningSessionController(MiningController mining, DroneController drone)
        {
            _mining = mining;
            _drone  = drone;
        }

        public void Start()   => EventBus.Subscribe<AsteroidSelectedEvent>(OnAsteroidSelected);

        public void Dispose()
        {
            EventBus.Unsubscribe<AsteroidSelectedEvent>(OnAsteroidSelected);
            DespawnVfx();
        }

        public void Tick()
        {
            var session = _mining.CurrentIdleSession;

            if (session != _trackedSession)
            {
                _trackedSession = session;
                if (session != null)
                {
                    _drone.SetTarget(session.Asteroid.transform);
                }
                else
                {
                    DespawnVfx();
                    _drone.ReturnToBase(); // asteroid claimed — head back to base
                }
            }

            if (session == null) return;

            switch (session.Stage)
            {
                case IdleMiningStage.Traveling:
                    if (_drone.IsAtTarget)
                    {
                        session.BeginMining();
                        // SpawnVfx(session.Asteroid.transform);
                        _mining.NotifyIdleSessionStageChanged();
                    }
                    break;

                case IdleMiningStage.Mining:
                    session.Tick(Time.deltaTime);
                    if (session.Stage == IdleMiningStage.ReadyToClaim)
                    {
                        // DespawnVfx();
                        _mining.NotifyIdleSessionStageChanged();
                    }
                    break;
            }
        }

        private void OnAsteroidSelected(AsteroidSelectedEvent e)
        {
            var session = _mining.CurrentIdleSession;
            if (session != null && session.Asteroid == e.Asteroid && session.Stage == IdleMiningStage.ReadyToClaim)
                _ = _mining.RegisterIdleClaimTapAsync(e.Asteroid);
        }

        private void SpawnVfx(Transform target)
        {
            DespawnVfx();

            var go = new GameObject("IdleMiningVFX");
            go.transform.SetParent(target, false);
            go.transform.localPosition = Vector3.zero;

            _vfx = go.AddComponent<ParticleSystem>();

            var main = _vfx.main;
            main.startLifetime = 0.6f;
            main.startSpeed    = 1.2f;
            main.startSize     = 0.08f;
            main.startColor    = new Color(1f, 0.65f, 0.2f);
            main.maxParticles  = 60;

            var emission = _vfx.emission;
            emission.rateOverTime = 25f;

            var shape = _vfx.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius    = 0.6f;

            var renderer = _vfx.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                      ?? Shader.Find("Particles/Standard Unlit")
                      ?? Shader.Find("Sprites/Default");
            if (shader != null)
                renderer.material = new Material(shader);

            _vfx.Play();
        }

        private void DespawnVfx()
        {
            if (_vfx == null) return;
            UnityEngine.Object.Destroy(_vfx.gameObject);
            _vfx = null;
        }
    }
}
