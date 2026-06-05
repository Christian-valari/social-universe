using UnityEngine;
using SocialUniverse.Config;

namespace SocialUniverse.Mining
{
    public class Asteroid : MonoBehaviour
    {
        public AsteroidDefinition Definition     { get; private set; }
        public int                RemainingYield { get; private set; }
        public bool               IsDepleted     => RemainingYield <= 0;

        public void Initialize(AsteroidDefinition definition)
        {
            Definition     = definition;
            RemainingYield = Mathf.RoundToInt(definition.BaseYield * Random.Range(0.8f, 1.2f));
        }

        public int Mine(int amount)
        {
            int actual = Mathf.Min(amount, RemainingYield);
            RemainingYield -= actual;
            return actual;
        }
    }
}
