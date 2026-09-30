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

        public void Initialize(AsteroidDefinition definition, string slotId, float yieldRollMin, float yieldRollMax)
        {
            Definition     = definition;
            SlotId         = slotId;
            RemainingYield = RollYield(definition.BaseYield, yieldRollMin, yieldRollMax, Random.value);

            if (GetComponent<Collider>() == null)
            {
                var collider = gameObject.AddComponent<SphereCollider>();
                collider.radius = 0.5f;
            }

            _rotationAxis  = Random.onUnitSphere;
            _rotationSpeed = Random.Range(_minRotationSpeed, _maxRotationSpeed);
        }

        // Spawn yield: BaseYield scaled by a roll in [min, max]. random01 is injected so tests can
        // pin it. ServerCode/ValidateMining.js caps grants assuming max is the ceiling.
        public static int RollYield(int baseYield, float min, float max, float random01) =>
            Mathf.RoundToInt(baseYield * Mathf.Lerp(min, max, random01));

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
