using UnityEngine;

namespace SocialUniverse.Mining
{
    public class DroneController : MonoBehaviour
    {
        [SerializeField] private float _travelSpeed = 5f;

        private Transform _target;

        public bool IsAtTarget =>
            _target == null || Vector3.Distance(transform.position, _target.position) < 0.2f;

        public void SetTarget(Transform target)
        {
            _target = target;
        }

        public void ReturnToBase(Vector3 basePosition)
        {
            transform.position = basePosition;
            _target            = null;
        }

        private void Update()
        {
            if (_target == null) return;
            transform.position = Vector3.MoveTowards(
                transform.position, _target.position, _travelSpeed * Time.deltaTime);
        }
    }
}
