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
            public string             SlotId;
            public DateTime           RespawnAtUtc;
        }

        // Distributes `fieldSize` slots across `types`, weighted by (1 - Rarity) per type —
        // rarer types get fewer slots. Uses largest-remainder rounding so the returned counts
        // always sum to exactly `fieldSize` (each type gets at least 1 slot when fieldSize
        // allows it). Pure and static so it's directly unit-testable without a scene.
        public static int[] DistributeFieldSize(AsteroidDefinition[] types, int fieldSize)
        {
            int n = types?.Length ?? 0;
            var counts = new int[n];
            if (n == 0 || fieldSize <= 0) return counts;

            var weights = new float[n];
            float totalWeight = 0f;
            for (int i = 0; i < n; i++)
            {
                weights[i]   = Mathf.Max(0.01f, 1f - types[i].Rarity);
                totalWeight += weights[i];
            }

            var raw       = new float[n];
            var remainder = new float[n];
            int assigned  = 0;

            for (int i = 0; i < n; i++)
            {
                raw[i]       = fieldSize * weights[i] / totalWeight;
                counts[i]    = Mathf.Max(1, Mathf.FloorToInt(raw[i]));
                remainder[i] = raw[i] - Mathf.Floor(raw[i]);
                assigned    += counts[i];
            }

            var byRemainderDesc = Enumerable.Range(0, n).OrderByDescending(i => remainder[i]).ToArray();

            int diff = fieldSize - assigned;
            int cursor = 0;
            while (diff > 0)
            {
                counts[byRemainderDesc[cursor % n]]++;
                diff--;
                cursor++;
            }

            cursor = 0;
            int guard = 0;
            while (diff < 0 && guard < n * 64)
            {
                int i = byRemainderDesc[n - 1 - (cursor % n)];
                if (counts[i] > 0) { counts[i]--; diff++; }
                cursor++;
                guard++;
            }

            return counts;
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

            var counts = DistributeFieldSize(planet.AsteroidTypes, planet.AsteroidFieldSize);

            for (int t = 0; t < planet.AsteroidTypes.Length; t++)
            {
                var def          = planet.AsteroidTypes[t];
                int targetCount  = counts[t];
                int pendingCount = _pending.Count(p => p.Definition == def);
                int toSpawn      = Mathf.Max(0, targetCount - pendingCount);

                for (int i = 0; i < toSpawn; i++)
                    SpawnOne(def, $"{def.MineralType}#{pendingCount + i}");
            }

            SULog.Info($"AsteroidSpawner: spawned {_active.Count} asteroids ({_pending.Count} pending respawn)", SULog.Channel.Mining);
        }

        public void ClearAll()
        {
            foreach (var a in _active)
            {
                if (a == null) continue;
                if (Application.isPlaying)
                    Destroy(a.gameObject);
                else
                    DestroyImmediate(a.gameObject);
            }
            _active.Clear();
        }

        // Destroys a claimed asteroid and schedules a same-type, same-slot replacement to
        // spawn after the cooldown.
        public void ScheduleRespawn(Asteroid asteroid, float respawnHours)
        {
            if (asteroid == null) return;

            var definition = asteroid.Definition;
            var slotId      = asteroid.SlotId;
            _active.Remove(asteroid);
            if (Application.isPlaying)
                Destroy(asteroid.gameObject);
            else
                DestroyImmediate(asteroid.gameObject);

            _pending.Add(new PendingRespawn
            {
                Definition   = definition,
                SlotId       = slotId,
                RespawnAtUtc = DateTime.UtcNow.AddHours(respawnHours)
            });
            SavePendingRespawns();

            SULog.Info($"Asteroid '{definition.MineralType}' claimed — respawns in {respawnHours:0.#}h", SULog.Channel.Mining);
        }

        // Returns the currently-active asteroid occupying the given slot, or null if it's
        // been claimed/is pending respawn. Used to reconcile a persisted idle-mining session
        // against the freshly spawned field after an app restart.
        public Asteroid FindBySlotId(string slotId)
        {
            foreach (var a in _active)
                if (a.SlotId == slotId) return a;
            return null;
        }

        private void Update()
        {
            if (_pending.Count == 0) return;

            var now     = DateTime.UtcNow;
            bool changed = false;

            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (now < _pending[i].RespawnAtUtc) continue;

                SpawnOne(_pending[i].Definition, _pending[i].SlotId);
                _pending.RemoveAt(i);
                changed = true;
            }

            if (changed) SavePendingRespawns();
        }

        private void SpawnOne(AsteroidDefinition def, string slotId)
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
            asteroid.Initialize(def, slotId);
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
                if (parts.Length != 3 || !long.TryParse(parts[2], out var unixSeconds)) continue;

                var definition = _registry.GetAsteroid(parts[0]);
                if (definition == null) continue;

                _pending.Add(new PendingRespawn
                {
                    Definition   = definition,
                    SlotId       = parts[1],
                    RespawnAtUtc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime
                });
            }
        }

        private void SavePendingRespawns()
        {
            var serialized = string.Join(";", _pending.Select(p =>
                $"{p.Definition.MineralType}|{p.SlotId}|{new DateTimeOffset(p.RespawnAtUtc).ToUnixTimeSeconds()}"));

            PlayerPrefs.SetString(SaveKeys.AsteroidRespawns, serialized);
            PlayerPrefs.Save();
        }
    }
}
