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

        [Inject] private EconomyConfig    _config;

        private readonly List<Asteroid>       _active  = new();
        // The current planet's emptied slots waiting to respawn: what the server's claim log reported
        // when the field was spawned (GetAsteroidRespawns — claims made on any device), plus this
        // session's own claims. Nothing is kept on the device; the next load asks the server again
        // (docs/superpowers/plans/2026-10-18-cloud-synced-planet-data.md).
        private readonly List<PendingRespawn> _pending = new();

        // The current planet's asteroid types: EnsureSlot only ever spawns one of these.
        private AsteroidDefinition[] _currentTypes = Array.Empty<AsteroidDefinition>();

        public IReadOnlyList<Asteroid> ActiveAsteroids => _active;

        // Returns the earliest scheduled respawn time on the current planet, or null if all of
        // its asteroids are live. Drives the HUD countdown.
        public DateTime? NextRespawnUtc
        {
            get
            {
                DateTime? next = null;
                foreach (var p in _pending)
                    if (next == null || p.RespawnAtUtc < next.Value)
                        next = p.RespawnAtUtc;
                return next;
            }
        }

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

        // respawns: the planet's slots the server says are still waiting to respawn (GetAsteroidRespawns);
        // null or empty spawns the whole field.
        public void SpawnForPlanet(PlanetDefinition planet, IReadOnlyList<AsteroidRespawn> respawns = null)
        {
            ClearAll();
            _pending.Clear();
            _currentTypes = planet.AsteroidTypes ?? Array.Empty<AsteroidDefinition>();
            ForgetDeviceRespawnTimers();
            AddServerRespawns(respawns);

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

                // Pending (claimed, awaiting-respawn) asteroids can occupy ANY index, not just
                // the lowest ones — a player can claim any asteroid of a type, not only the
                // lowest-indexed one. Reserve whichever indices pending entries actually hold
                // (parsed from their SlotId) and assign new spawns the lowest indices NOT
                // already reserved, so a new spawn never collides with — or displaces — a
                // pending entry's eventual respawn slot.
                var reservedIndices = new HashSet<int>(
                    _pending.Where(p => p.Definition == def)
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
            var position    = asteroid.transform.position;
            _active.Remove(asteroid);
            if (Application.isPlaying)
                Destroy(asteroid.gameObject);
            else
                DestroyImmediate(asteroid.gameObject);

            SpawnDestroyVfx(position);

            // The server's claim log already has this claim (if it was granted), so a reload brings
            // the same timer back from GetAsteroidRespawns; nothing is saved here.
            _pending.Add(new PendingRespawn
            {
                Definition   = definition,
                SlotId       = slotId,
                RespawnAtUtc = DateTime.UtcNow.AddHours(respawnHours)
            });

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

        // A server-tracked idle run (started on any device) names its asteroid by slot. The slot may
        // still be waiting to respawn here (a claim this session made, or a server timer that clock
        // skew left running): drop that timer and spawn the slot now. Returns the slot's asteroid, or
        // null when its type isn't one of the current planet's (or the id is malformed).
        public Asteroid EnsureSlot(string slotId)
        {
            var live = FindBySlotId(slotId);
            if (live != null) return live;

            var definition = TypeOfSlot(slotId);
            if (definition == null) return null;

            _pending.RemoveAll(p => p.SlotId == slotId);

            SULog.Info($"AsteroidSpawner: spawned {slotId} now for an idle run in progress", SULog.Channel.Mining);
            return SpawnOne(definition, slotId);
        }

        private void Update()
        {
            if (_pending.Count == 0) return;

            var now = DateTime.UtcNow;

            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (now < _pending[i].RespawnAtUtc) continue;

                SpawnOne(_pending[i].Definition, _pending[i].SlotId);
                _pending.RemoveAt(i);
            }
        }

        private Asteroid SpawnOne(AsteroidDefinition def, string slotId)
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
            return asteroid;
        }

        private Vector3 RandomOrbitPoint() => UnityEngine.Random.onUnitSphere * _orbitRadius;

        private void SpawnDestroyVfx(Vector3 position)
        {
            if (_destroyVfxPrefab == null || !Application.isPlaying) return;

            var vfx = Instantiate(_destroyVfxPrefab, position, Quaternion.identity);
            Destroy(vfx, _destroyVfxLifetime);
        }

        // Extracts the numeric index from a "{mineral}#{index}" SlotId. Returns -1 if the
        // slot ID is null/malformed rather than throwing, so a corrupt persisted entry just
        // fails to reserve an index instead of breaking the whole spawn pass.
        private static int ParseSlotIndex(string slotId)
        {
            int hashIdx = slotId?.LastIndexOf('#') ?? -1;
            if (hashIdx < 0) return -1;
            return int.TryParse(slotId.Substring(hashIdx + 1), out int idx) ? idx : -1;
        }

        // The current planet's asteroid type a "{mineralType}#{index}" slot id names, or null if the
        // id is malformed or names a type this planet doesn't have.
        private AsteroidDefinition TypeOfSlot(string slotId)
        {
            int hashIdx = slotId?.LastIndexOf('#') ?? -1;
            if (hashIdx <= 0 || ParseSlotIndex(slotId) < 0) return null;

            string mineralType = slotId.Substring(0, hashIdx);
            return Array.Find(_currentTypes, t => t != null && t.MineralType == mineralType);
        }

        // A slot this planet can't hold is skipped, and one already due is left to the normal fill. A
        // slot reported twice (claimed, brought back for an idle run, claimed again) is one empty
        // slot, back when its later timer ends.
        private void AddServerRespawns(IReadOnlyList<AsteroidRespawn> respawns)
        {
            if (respawns == null) return;

            var now = DateTime.UtcNow;
            foreach (var r in respawns)
            {
                if (r == null || r.SecondsLeft <= 0f) continue;
                var definition = TypeOfSlot(r.SlotId);
                if (definition == null) continue;

                var respawnAt = now.AddSeconds(r.SecondsLeft);
                int existing  = _pending.FindIndex(p => p.SlotId == r.SlotId);
                if (existing >= 0 && _pending[existing].RespawnAtUtc >= respawnAt) continue;

                var entry = new PendingRespawn { Definition = definition, SlotId = r.SlotId, RespawnAtUtc = respawnAt };
                if (existing >= 0) _pending[existing] = entry;
                else               _pending.Add(entry);
            }
        }

        // Builds before cloud-synced respawns kept the timers in this device's PlayerPrefs, shared by
        // every account on it. The server's claim log replaced them, so the leftover is deleted.
        private static void ForgetDeviceRespawnTimers()
        {
            if (!PlayerPrefs.HasKey(SaveKeys.AsteroidRespawns)) return;
            PlayerPrefs.DeleteKey(SaveKeys.AsteroidRespawns);
            PlayerPrefs.Save();
        }
    }
}
