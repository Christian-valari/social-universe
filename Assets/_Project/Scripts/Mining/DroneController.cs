using UnityEngine;

namespace SocialUniverse.Mining
{
    public class DroneController : MonoBehaviour
    {
        [SerializeField] private float _travelSpeed  = 5f;
        [SerializeField] private float _stopDistance = 1.5f;
        [SerializeField] private float _orbitSpeed   = 60f;  // degrees per second while mining
        [SerializeField] private float _turnSpeed    = 180f; // degrees per second while traveling

        [Header("Model")]
        [SerializeField] private Transform _modelAnchor; // where the drone model spawns; defaults to this transform

        [Header("Base")]
        [SerializeField] private Transform _baseAnchor; // if unset, the drone's starting position is used

        private enum Activity { Idle, Traveling, Mining }

        // Bound at runtime from the active drone's model (DroneModelView), since the model is swapped
        // to match the selected drone — the rig can't hold fixed VFX references.
        private ParticleSystem _travelingEffect;
        private ParticleSystem _miningEffect;
        private GameObject     _modelInstance;

        private Transform _target;
        private bool      _orbiting;
        private bool      _isMoving;
        private bool      _returningToBase;
        private float     _orbitAngle;
        private Activity  _activity = Activity.Idle;

        public bool IsAtTarget =>
            _target == null || Vector3.Distance(transform.position, _target.position) <= _stopDistance + 0.05f;

        private void Awake()
        {
            if (_baseAnchor == null)
            {
                var anchor = new GameObject("DroneBaseAnchor") { hideFlags = HideFlags.HideInHierarchy };
                anchor.transform.SetParent(transform.parent, true);
                anchor.transform.position = transform.position;
                _baseAnchor = anchor.transform;
            }
        }

        // Swaps in the model for the currently-selected drone. Destroys the previous model, spawns
        // the new one under the anchor, and rebinds the traveling/mining VFX from its DroneModelView.
        // A null prefab is a no-op (keeps whatever is showing) — used as a fallback when a drone has
        // no model assigned.
        public void SetModel(GameObject prefab)
        {
            if (prefab == null) return;

            if (_modelInstance != null) Destroy(_modelInstance);

            Transform anchor = _modelAnchor != null ? _modelAnchor : transform;
            _modelInstance = Instantiate(prefab, anchor);
            _modelInstance.transform.localPosition = Vector3.zero;
            _modelInstance.transform.localRotation = Quaternion.identity;

            var view = _modelInstance.GetComponent<DroneModelView>();
            _travelingEffect = view != null ? view.TravelingEffect : null;
            _miningEffect    = view != null ? view.MiningEffect    : null;

            // Start both effects off and force the activity to re-apply on the next Update so the new
            // model's VFX match the current state (idle/travel/mine).
            SetEffect(_travelingEffect, false);
            SetEffect(_miningEffect, false);
            _activity = Activity.Idle;
        }

        public void SetTarget(Transform target)
        {
            _target          = target;
            _orbiting        = false;
            _isMoving        = true;
            _returningToBase = false;
        }

        // Places the drone directly at the target with no travel animation or VFX — used when
        // an idle session is restored (e.g. app relaunch), since the travel already
        // conceptually happened off-screen while the app was closed.
        public void SnapToTarget(Transform target)
        {
            _target          = target;
            _orbiting        = false;
            _isMoving        = false;
            _returningToBase = false;

            if (target != null)
            {
                transform.position = target.position + Vector3.forward * _stopDistance;
                transform.LookAt(target.position);
            }
        }

        // Sends the drone back to its base anchor; it travels there and then idles (no orbit/VFX).
        public void ReturnToBase()
        {
            _target          = _baseAnchor;
            _orbiting        = false;
            _isMoving        = true;
            _returningToBase = true;
        }

        private void Update()
        {
            if (_target == null)
            {
                SetActivity(Activity.Idle);
                return;
            }

            if (Vector3.Distance(transform.position, _target.position) > _stopDistance && _isMoving)
            {
                _orbiting = false;
                SetActivity(Activity.Traveling);
                transform.position = Vector3.MoveTowards(
                    transform.position, _target.position, _travelSpeed * Time.deltaTime);
                FaceTowards(_target.position);
                return;
            }

            _isMoving = false;

            if (_returningToBase)
            {
                transform.position = _target.position;
                _target            = null;
                _returningToBase   = false;
                SetActivity(Activity.Idle);
                return;
            }

            SetActivity(Activity.Mining);
            OrbitTarget();
        }

        private void SetActivity(Activity activity)
        {
            if (_activity == activity) return;
            _activity = activity;

            SetEffect(_travelingEffect, activity == Activity.Traveling);
            SetEffect(_miningEffect, activity == Activity.Mining);
        }

        private static void SetEffect(ParticleSystem effect, bool play)
        {
            if (effect == null) return;
            if (play) effect.Play();
            else       effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // Smoothly turns the drone to face its destination while traveling.
        private void FaceTowards(Vector3 point)
        {
            Vector3 direction = point - transform.position;
            if (direction.sqrMagnitude < 0.0001f) return;

            Quaternion desired = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, _turnSpeed * Time.deltaTime);
        }

        // Slowly circles the target at the stop distance to simulate active mining.
        private void OrbitTarget()
        {
            if (!_orbiting)
            {
                _orbiting   = true;
                Vector3 toSelf = transform.position - _target.position;
                _orbitAngle = Mathf.Atan2(toSelf.x, toSelf.z) * Mathf.Rad2Deg;
            }

            _orbitAngle = Mathf.Repeat(_orbitAngle + _orbitSpeed * Time.deltaTime, 360f);

            Vector3 offset = Quaternion.Euler(0f, _orbitAngle, 0f) * (Vector3.forward * _stopDistance);
            transform.position = _target.position + offset;
            transform.LookAt(_target.position);
        }
    }
}
