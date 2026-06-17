using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;
using SocialUniverse.Config;
using SocialUniverse.Core;

namespace SocialUniverse.Mining
{
    public class AsteroidSpawner : MonoBehaviour
    {
        [SerializeField] private float _orbitRadius   = 15f;
        [SerializeField] private int   _maxPerType    = 4;

        [Inject] private DatabaseRegistry _registry;

        private readonly List<Asteroid>       _active  = new();
        private readonly List<PendingRespawn> _pending = new();

        public IReadOnlyList<Asteroid> ActiveAsteroids => _active;

        // Returns the earliest scheduled respawn time, or null if all asteroids are live.
        public DateTime? NextRespawnUtc =>
            _pending.Count > 0 ? _pending.Min(p => p.RespawnAtUtc) : (DateTime?)null;

        private struct PendingRespawn
        {
            public AsteroidDefinition Definition;
            public DateTime           RespawnAtUtc;
        }

        public void SpawnForPlanet(PlanetDefinition planet)
        {
            ClearAll();
            LoadPendingRespawns();

            if (planet.AsteroidTypes == null || planet.AsteroidTypes.Length == 0)
            {
                SULog.Warn($"Planet '{planet.DisplayName}' has no asteroid types defined", SULog.Channel.Mining);
                return;
            }

            foreach (var def in planet.AsteroidTypes)
            {
                int targetCount  = Mathf.Max(1, Mathf.RoundToInt(_maxPerType * (1f - def.Rarity)));
                int pendingCount = _pending.Count(p => p.Definition == def);
                int toSpawn      = Mathf.Max(0, targetCount - pendingCount);

                for (int i = 0; i < toSpawn; i++)
                    SpawnOne(def);
            }

            SULog.Info($"AsteroidSpawner: spawned {_active.Count} asteroids ({_pending.Count} pending respawn)", SULog.Channel.Mining);
        }

        public void ClearAll()
        {
            foreach (var a in _active)
                if (a != null) Destroy(a.gameObject);
            _active.Clear();
        }

        // Destroys a claimed asteroid and schedules a same-type replacement to spawn after the cooldown.
        public void ScheduleRespawn(Asteroid asteroid, float respawnHours)
        {
            if (asteroid == null) return;

            var definition = asteroid.Definition;
            _active.Remove(asteroid);
            Destroy(asteroid.gameObject);

            _pending.Add(new PendingRespawn
            {
                Definition   = definition,
                RespawnAtUtc = DateTime.UtcNow.AddHours(respawnHours)
            });
            SavePendingRespawns();

            SULog.Info($"Asteroid '{definition.MineralType}' claimed — respawns in {respawnHours:0.#}h", SULog.Channel.Mining);
        }

        private void Update()
        {
            if (_pending.Count == 0) return;

            var now     = DateTime.UtcNow;
            bool changed = false;

            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (now < _pending[i].RespawnAtUtc) continue;

                SpawnOne(_pending[i].Definition);
                _pending.RemoveAt(i);
                changed = true;
            }

            if (changed) SavePendingRespawns();
        }

        private void SpawnOne(AsteroidDefinition def)
        {
            GameObject go;
            if (def.ModelPrefab != null)
            {
                go = Instantiate(def.ModelPrefab, RandomOrbitPoint(), UnityEngine.Random.rotation, transform);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.SetParent(transform);
                go.transform.position   = RandomOrbitPoint();
                go.transform.rotation   = UnityEngine.Random.rotation;
                go.transform.localScale = Vector3.one * 0.5f;
            }

            go.name = $"Asteroid_{def.MineralType}";
            var asteroid = go.AddComponent<Asteroid>();
            asteroid.Initialize(def);
            _active.Add(asteroid);
        }

        private Vector3 RandomOrbitPoint() => UnityEngine.Random.onUnitSphere * _orbitRadius;

        private void LoadPendingRespawns()
        {
            _pending.Clear();

            var raw = PlayerPrefs.GetString(SaveKeys.AsteroidRespawns, "");
            if (string.IsNullOrEmpty(raw)) return;

            foreach (var entry in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = entry.Split('|');
                if (parts.Length != 2 || !long.TryParse(parts[1], out var unixSeconds)) continue;

                var definition = _registry.GetAsteroid(parts[0]);
                if (definition == null) continue;

                _pending.Add(new PendingRespawn
                {
                    Definition   = definition,
                    RespawnAtUtc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime
                });
            }
        }

        private void SavePendingRespawns()
        {
            var serialized = string.Join(";", _pending.Select(p =>
                $"{p.Definition.MineralType}|{new DateTimeOffset(p.RespawnAtUtc).ToUnixTimeSeconds()}"));

            PlayerPrefs.SetString(SaveKeys.AsteroidRespawns, serialized);
            PlayerPrefs.Save();
        }
    }
}
