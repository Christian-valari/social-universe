using UnityEngine;
using SocialUniverse.Config;

namespace SocialUniverse.Mining
{
    public class AsteroidSelectedEvent { public Asteroid Asteroid; }

    public class Asteroid : MonoBehaviour
    {
        [SerializeField] private float _minRotationSpeed = 5f;  // degrees per second
        [SerializeField] private float _maxRotationSpeed = 20f;

        public AsteroidDefinition Definition     { get; private set; }
        public string             SlotId         { get; private set; }
        public int                RemainingYield { get; private set; }
        public bool                IsDepleted     => RemainingYield <= 0;

        private Vector3 _rotationAxis;
        private float   _rotationSpeed;

        public void Initialize(AsteroidDefinition definition, string slotId)
        {
            Definition     = definition;
            SlotId         = slotId;
            RemainingYield = Mathf.RoundToInt(definition.BaseYield * Random.Range(0.8f, 1.2f));

            if (GetComponent<Collider>() == null)
            {
                var collider = gameObject.AddComponent<SphereCollider>();
                collider.radius = 0.5f;
            }

            _rotationAxis  = Random.onUnitSphere;
            _rotationSpeed = Random.Range(_minRotationSpeed, _maxRotationSpeed);
        }

        public int Mine(int amount)
        {
            int actual = Mathf.Min(amount, RemainingYield);
            RemainingYield -= actual;
            return actual;
        }

        // Slow tumble to make asteroids feel alive in the field.
        private void Update()
        {
            transform.Rotate(_rotationAxis, _rotationSpeed * Time.deltaTime, Space.World);
        }
    }
}
