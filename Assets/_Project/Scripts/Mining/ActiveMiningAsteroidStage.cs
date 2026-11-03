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
        [SerializeField] private float _shakeDistance    = 0.002f; // world units at the start of a shake

        public GameObject StageClone    { get; private set; }
        public float      ColliderRadius { get; private set; }

        // The largest sphere about the clone's pivot that fits inside its mesh's bounds, so a point
        // on it projects over the rock. ColliderRadius is the prefabs' sphere instead, which encloses
        // the rock and leaves a point on it hanging in space.
        public float      SurfaceRadius  { get; private set; }

        private Vector3 _rotationAxis;
        private float   _rotationSpeed;

        private Vector3 _restLocalPosition; // where the clone goes back to once a shake ends
        private float   _shakeSeconds;
        private float   _shakeSecondsLeft;

        // Instantiates definition.ModelPrefab (or a fallback primitive sphere, matching
        // AsteroidSpawner's fallback) as a child of this transform, and records SurfaceRadius,
        // which target-point placement uses.
        public GameObject SpawnClone(AsteroidDefinition definition)
        {
            if (StageClone != null)
            {
                if (Application.isPlaying) Destroy(StageClone);
                else                       DestroyImmediate(StageClone);
                _shakeSecondsLeft = 0f; // a shake goes with its clone
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
            SurfaceRadius  = MeasureSurfaceRadius(clone, ColliderRadius);

            _rotationAxis  = Random.onUnitSphere;
            _rotationSpeed = Random.Range(_minRotationSpeed, _maxRotationSpeed);

            StageClone = clone;
            return clone;
        }

        // Where the clone's current spin carries a world-space point after this many seconds. The
        // spin turns about where the clone rests, which a shake only moves it away from for a moment.
        public Vector3 PredictPoint(Vector3 worldPoint, float seconds)
        {
            if (StageClone == null) return worldPoint;

            Vector3 center  = StageClone.transform.position;
            Vector3 resting = _shakeSecondsLeft > 0f ? transform.TransformPoint(_restLocalPosition) : center;
            return resting + Quaternion.AngleAxis(_rotationSpeed * seconds, _rotationAxis) * (worldPoint - center);
        }

        // Jitters the clone's position for this long, easing out, then puts it back exactly where it
        // started. A shake already under way is only replaced by a longer one.
        public void Shake(float seconds)
        {
            if (StageClone == null || seconds <= _shakeSecondsLeft) return;

            if (_shakeSecondsLeft <= 0f) _restLocalPosition = StageClone.transform.localPosition;
            _shakeSeconds     = seconds;
            _shakeSecondsLeft = seconds;
        }

        // Slow tumble to match the atmosphere of the field asteroids (Asteroid.Update()).
        private void Update()
        {
            if (StageClone != null)
                StageClone.transform.Rotate(_rotationAxis, _rotationSpeed * Time.deltaTime, Space.World);

            AdvanceShake(Time.deltaTime);
        }

        // Reads only the mesh's bounds, which stay readable on a device where its vertices may not.
        // The asteroid prefabs keep the mesh on a child that sits off the root's pivot, so the distance
        // from the pivot to the bounds' centre is taken in the world, not from the mesh's own origin.
        // With no mesh, or a pivot outside its bounds, half the collider's sphere is a safe guess.
        private static float MeasureSurfaceRadius(GameObject clone, float colliderRadius)
        {
            float fallback = colliderRadius * 0.5f;

            var filter = clone.GetComponentInChildren<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return fallback;

            Bounds  bounds = filter.sharedMesh.bounds;
            Vector3 scale  = filter.transform.lossyScale;
            Vector3 centre = filter.transform.TransformPoint(bounds.center) - clone.transform.position;

            float radius = Mathf.Min(
                bounds.extents.x * Mathf.Abs(scale.x) - Mathf.Abs(centre.x),
                bounds.extents.y * Mathf.Abs(scale.y) - Mathf.Abs(centre.y),
                bounds.extents.z * Mathf.Abs(scale.z) - Mathf.Abs(centre.z));
            return radius > 0f ? radius : fallback;
        }

        private void AdvanceShake(float deltaTime)
        {
            if (StageClone == null || _shakeSecondsLeft <= 0f) return;

            _shakeSecondsLeft -= deltaTime;
            StageClone.transform.localPosition = _shakeSecondsLeft > 0f
                ? _restLocalPosition + Random.onUnitSphere * (_shakeDistance * _shakeSecondsLeft / _shakeSeconds)
                : _restLocalPosition;
        }
    }
}
