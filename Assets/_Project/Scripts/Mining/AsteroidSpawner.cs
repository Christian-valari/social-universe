using System.Collections.Generic;
using UnityEngine;
using SocialUniverse.Config;
using SocialUniverse.Core;

namespace SocialUniverse.Mining
{
    public class AsteroidSpawner : MonoBehaviour
    {
        [SerializeField] private float _orbitRadius   = 15f;
        [SerializeField] private int   _maxPerType    = 4;

        private readonly List<Asteroid> _active = new();

        public IReadOnlyList<Asteroid> ActiveAsteroids => _active;

        public void SpawnForPlanet(PlanetDefinition planet)
        {
            ClearAll();

            if (planet.AsteroidTypes == null || planet.AsteroidTypes.Length == 0)
            {
                SULog.Warn($"Planet '{planet.DisplayName}' has no asteroid types defined", SULog.Channel.Mining);
                return;
            }

            foreach (var def in planet.AsteroidTypes)
            {
                int count = Mathf.Max(1, Mathf.RoundToInt(_maxPerType * (1f - def.Rarity)));
                for (int i = 0; i < count; i++)
                    SpawnOne(def);
            }

            SULog.Info($"AsteroidSpawner: spawned {_active.Count} asteroids", SULog.Channel.Mining);
        }

        public void ClearAll()
        {
            foreach (var a in _active)
                if (a != null) Destroy(a.gameObject);
            _active.Clear();
        }

        private void SpawnOne(AsteroidDefinition def)
        {
            GameObject go;
            if (def.ModelPrefab != null)
            {
                go = Instantiate(def.ModelPrefab, RandomOrbitPoint(), Random.rotation, transform);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.SetParent(transform);
                go.transform.position   = RandomOrbitPoint();
                go.transform.rotation   = Random.rotation;
                go.transform.localScale = Vector3.one * 0.5f;
            }

            go.name = $"Asteroid_{def.MineralType}";
            var asteroid = go.AddComponent<Asteroid>();
            asteroid.Initialize(def);
            _active.Add(asteroid);
        }

        private Vector3 RandomOrbitPoint() => Random.onUnitSphere * _orbitRadius;
    }
}
