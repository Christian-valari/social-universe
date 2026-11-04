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
        private const float SpinEaseSeconds = 0.2f; // how long the spin takes to reach a new speed

        // The tumble before a run starts; Start replaces the range with the run's own (SetSpinRange).
        [SerializeField] private float _minRotationSpeed = 5f;  // degrees per second
        [SerializeField] private float _maxRotationSpeed = 15f;
        [SerializeField] private float _shakeDistance    = 0.002f; // world units at the start of a shake

        public GameObject StageClone    { get; private set; }
        public float      ColliderRadius { get; private set; }

        // Degrees per second the clone is turning at.
        public float      RotationSpeed  => _rotationSpeed;

        // The largest sphere about the clone's pivot that fits inside its mesh's bounds, so a point
        // on it projects over the rock. ColliderRadius is the prefabs' sphere instead, which encloses
        // the rock and leaves a point on it hanging in space.
        public float      SurfaceRadius  { get; private set; }

        private Vector3 _rotationAxis;
        private float   _rotationSpeed;

        private float   _spinGoal;        // the speed an ease is heading for
        private float   _spinEaseRate;    // degrees per second gained or lost each second; 0 when not easing
        private float   _spinToRestore;   // what RestoreSpin goes back to
        private bool    _spinEased;       // an EaseSpinTo is waiting on its RestoreSpin

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
            SetSpeed(Random.Range(_minRotationSpeed, _maxRotationSpeed));

            StageClone = clone;
            return clone;
        }

        // Sets how fast the clone may spin and picks a new speed in that range, for the clone on
        // stage and for any spawned after.
        public void SetSpinRange(float minDegrees, float maxDegrees)
        {
            _minRotationSpeed = minDegrees;
            _maxRotationSpeed = maxDegrees;
            SetSpeed(Random.Range(minDegrees, maxDegrees));
        }

        // Takes the spin to this speed over a moment. RestoreSpin puts back the speed from before the
        // first ease, however many follow it.
        public void EaseSpinTo(float degrees)
        {
            if (!_spinEased)
            {
                _spinToRestore = SettlingSpeed;
                _spinEased     = true;
            }
            BeginEase(degrees);
        }

        public void RestoreSpin()
        {
            if (!_spinEased) return;

            _spinEased = false;
            BeginEase(_spinToRestore);
        }

        // Where the clone's spin carries a world-space point after this many seconds, at the speed it
        // is settling at, since an ease is over long before a ring closes. The spin turns about where
        // the clone rests, which a shake only moves it away from for a moment.
        public Vector3 PredictPoint(Vector3 worldPoint, float seconds)
        {
            if (StageClone == null) return worldPoint;

            Vector3 center  = StageClone.transform.position;
            Vector3 resting = _shakeSecondsLeft > 0f ? transform.TransformPoint(_restLocalPosition) : center;
            return resting + Quaternion.AngleAxis(SettlingSpeed * seconds, _rotationAxis) * (worldPoint - center);
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
            AdvanceSpin(Time.deltaTime);

            if (StageClone != null)
                StageClone.transform.Rotate(_rotationAxis, _rotationSpeed * Time.deltaTime, Space.World);

            AdvanceShake(Time.deltaTime);
        }

        private float SettlingSpeed => _spinEaseRate > 0f ? _spinGoal : _rotationSpeed;

        // A speed picked outright replaces any ease, and with it what the ease would restore.
        private void SetSpeed(float degrees)
        {
            _rotationSpeed = degrees;
            _spinEaseRate  = 0f;
            _spinEased     = false;
        }

        private void BeginEase(float degrees)
        {
            _spinGoal     = degrees;
            _spinEaseRate = Mathf.Abs(_rotationSpeed - degrees) / SpinEaseSeconds;
        }

        private void AdvanceSpin(float deltaTime)
        {
            if (_spinEaseRate <= 0f) return;

            _rotationSpeed = Mathf.MoveTowards(_rotationSpeed, _spinGoal, _spinEaseRate * deltaTime);
            if (Mathf.Approximately(_rotationSpeed, _spinGoal))
            {
                _rotationSpeed = _spinGoal;
                _spinEaseRate  = 0f;
            }
        }

        // Reads only the mesh's bounds, which stay readable on a device where its vertices may not.
        // The asteroid prefabs keep the mesh on a child that sits off the root's pivot, so the distance
        // from the pivot to the bounds' centre is taken in the world, not from the mesh's own origin.
        // With no mesh, or a pivot outside its bounds, half the collider's sphere is a safe guess.
        // This assumes the mesh child isn't rotated relative to the clone, since the extents are
        // compared along the clone's axes, and it measures the first mesh found only.
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
