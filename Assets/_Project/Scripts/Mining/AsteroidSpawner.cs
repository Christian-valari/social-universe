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

        [Header("VFX")]
        [SerializeField] private GameObject _destroyVfxPrefab;
        [SerializeField] private float      _destroyVfxLifetime = 2f;

        [Inject] private DatabaseRegistry _registry;
        [Inject] private EconomyConfig    _config;

        private readonly List<Asteroid>       _active  = new();
        // Pending respawns for ALL planets. Timers are per planet: only the current planet's
        // entries reduce/reserve its field or respawn into it; other planets' entries are kept
        // (and persisted) until that planet is loaded again.
        private readonly List<PendingRespawn> _pending = new();

        // Planet the field was last spawned for (SpawnForPlanet); stamped on new claims.
        private string _currentPlanetId;

        public IReadOnlyList<Asteroid> ActiveAsteroids => _active;

        // Returns the earliest scheduled respawn time on the current planet, or null if all of
        // its asteroids are live. Drives the HUD countdown.
        public DateTime? NextRespawnUtc
        {
            get
            {
                DateTime? next = null;
                foreach (var p in _pending)
                    if (IsCurrentPlanet(p) && (next == null || p.RespawnAtUtc < next.Value))
                        next = p.RespawnAtUtc;
                return next;
            }
        }

        private struct PendingRespawn
        {
            public string             PlanetId;
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
            _currentPlanetId = planet.PlanetId;
            LoadPendingRespawns();

            // The current planet's EXPIRED entries are no longer pending: drop them and let the
            // normal fill below spawn them, so the field size stays exact.
            var now = DateTime.UtcNow;
            if (_pending.RemoveAll(p => IsCurrentPlanet(p) && now >= p.RespawnAtUtc) > 0)
                SavePendingRespawns();

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
                int pendingCount = _pending.Count(p => IsCurrentPlanet(p) && p.Definition == def);
                int toSpawn      = Mathf.Max(0, targetCount - pendingCount);

                // Pending (claimed, awaiting-respawn) asteroids can occupy ANY index, not just
                // the lowest ones — a player can claim any asteroid of a type, not only the
                // lowest-indexed one. Reserve whichever indices pending entries actually hold
                // (parsed from their SlotId) and assign new spawns the lowest indices NOT
                // already reserved, so a new spawn never collides with — or displaces — a
                // pending entry's eventual respawn slot.
                var reservedIndices = new HashSet<int>(
                    _pending.Where(p => IsCurrentPlanet(p) && p.Definition == def)
                            .Select(p => ParseSlotIndex(p.SlotId))
                            .Where(idx => idx >= 0));

                int nextIndex = 0;
                for (int i = 0; i < toSpawn; i++)
                {
                    while (reservedIndices.Contains(nextIndex)) nextIndex++;
                    SpawnOne(def, $"{def.MineralType}#{nextIndex}");
                    reservedIndices.Add(nextIndex);
                    nextIndex++;
                }
            }

            SULog.Info($"AsteroidSpawner: spawned {_active.Count} asteroids ({_pending.Count(IsCurrentPlanet)} pending respawn)", SULog.Channel.Mining);
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
            var position    = asteroid.transform.position;
            _active.Remove(asteroid);
            if (Application.isPlaying)
                Destroy(asteroid.gameObject);
            else
                DestroyImmediate(asteroid.gameObject);

            SpawnDestroyVfx(position);

            _pending.Add(new PendingRespawn
            {
                PlanetId     = _currentPlanetId,
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
                // Other planets' expired entries stay persisted; they are resolved when that
                // planet is loaded (SpawnForPlanet).
                if (!IsCurrentPlanet(_pending[i]) || now < _pending[i].RespawnAtUtc) continue;

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
            asteroid.Initialize(def, slotId, _config.AsteroidYieldRollMin, _config.AsteroidYieldRollMax);
            _active.Add(asteroid);
        }

        private Vector3 RandomOrbitPoint() => UnityEngine.Random.onUnitSphere * _orbitRadius;

        private void SpawnDestroyVfx(Vector3 position)
        {
            if (_destroyVfxPrefab == null || !Application.isPlaying) return;

            var vfx = Instantiate(_destroyVfxPrefab, position, Quaternion.identity);
            Destroy(vfx, _destroyVfxLifetime);
        }

        private bool IsCurrentPlanet(PendingRespawn p) => p.PlanetId == _currentPlanetId;

        // Extracts the numeric index from a "{mineral}#{index}" SlotId. Returns -1 if the
        // slot ID is null/malformed rather than throwing, so a corrupt persisted entry just
        // fails to reserve an index instead of breaking the whole spawn pass.
        private static int ParseSlotIndex(string slotId)
        {
            int hashIdx = slotId?.LastIndexOf('#') ?? -1;
            if (hashIdx < 0) return -1;
            return int.TryParse(slotId.Substring(hashIdx + 1), out int idx) ? idx : -1;
        }

        private void LoadPendingRespawns()
        {
            _pending.Clear();

            var raw = PlayerPrefs.GetString(SaveKeys.AsteroidRespawns, "");
            if (string.IsNullOrEmpty(raw)) return;

            // Format: "{planetId}|{mineralType}|{slotId}|{unixSeconds}" joined by ';'. Legacy
            // 3-part entries (no planet id, pre-claim-budget) are dropped — they can't be
            // attributed to a planet, and no build writing them has shipped.
            foreach (var entry in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = entry.Split('|');
                if (parts.Length != 4 || string.IsNullOrEmpty(parts[0])
                    || !long.TryParse(parts[3], out var unixSeconds)) continue;

                var definition = _registry.GetAsteroid(parts[1]);
                if (definition == null) continue;

                _pending.Add(new PendingRespawn
                {
                    PlanetId     = parts[0],
                    Definition   = definition,
                    SlotId       = parts[2],
                    RespawnAtUtc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime
                });
            }
        }

        private void SavePendingRespawns()
        {
            var serialized = string.Join(";", _pending.Select(p =>
                $"{p.PlanetId}|{p.Definition.MineralType}|{p.SlotId}|{new DateTimeOffset(p.RespawnAtUtc).ToUnixTimeSeconds()}"));

            PlayerPrefs.SetString(SaveKeys.AsteroidRespawns, serialized);
            PlayerPrefs.Save();
        }
    }
}
