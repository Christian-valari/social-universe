using UnityEngine;
using SocialUniverse.Config;

namespace SocialUniverse.Mining
{
    // Spawns a visual clone of an asteroid's model prefab for the active-mining minigame scene.
    // The clone is presentation-only — ActiveMiningHandoff (populated back in Planet before the
    // scene swap) is the single source of truth for RemainingYield/Definition; this never
    // touches the original field Asteroid instance back in the Planet scene.
    public class ActiveMiningAsteroidStage : MonoBehaviour
    {
        [SerializeField] private float _minRotationSpeed = 5f;  // degrees per second
        [SerializeField] private float _maxRotationSpeed = 15f;

        public GameObject StageClone    { get; private set; }
        public float      ColliderRadius { get; private set; }

        private Vector3 _rotationAxis;
        private float   _rotationSpeed;

        // Instantiates definition.ModelPrefab (or a fallback primitive sphere, matching
        // AsteroidSpawner's fallback) as a child of this transform, and records the collider
        // radius used for target-point placement.
        public GameObject SpawnClone(AsteroidDefinition definition)
        {
            if (StageClone != null)
            {
                if (Application.isPlaying) Destroy(StageClone);
                else                       DestroyImmediate(StageClone);
            }

            GameObject clone;
            if (definition.ModelPrefab != null)
            {
                clone = Instantiate(definition.ModelPrefab, transform.position, Quaternion.identity, transform);
            }
            else
            {
                clone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                clone.transform.SetParent(transform);
                clone.transform.localPosition = Vector3.zero;
                clone.transform.localScale    = Vector3.one * 0.5f;
            }

            var collider = clone.GetComponent<Collider>();
            if (collider == null)
            {
                var sphere = clone.AddComponent<SphereCollider>();
                sphere.radius = 0.5f;
                collider = sphere;
            }
            ColliderRadius = Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.y, collider.bounds.extents.z);

            _rotationAxis  = Random.onUnitSphere;
            _rotationSpeed = Random.Range(_minRotationSpeed, _maxRotationSpeed);

            StageClone = clone;
            return clone;
        }

        // Slow tumble to match the atmosphere of the field asteroids (Asteroid.Update()).
        private void Update()
        {
            if (StageClone != null)
                StageClone.transform.Rotate(_rotationAxis, _rotationSpeed * Time.deltaTime, Space.World);
        }
    }
}
