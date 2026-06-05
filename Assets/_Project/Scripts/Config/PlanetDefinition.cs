using UnityEngine;

namespace SocialUniverse.Config
{
    [CreateAssetMenu(menuName = "SocialUniverse/Config/PlanetDefinition", fileName = "NewPlanet")]
    public class PlanetDefinition : ScriptableObject
    {
        [SerializeField] private string _planetId;
        [SerializeField] private string _displayName;
        [SerializeField] private GameObject _modelPrefab;
        [SerializeField] private int _tileCount = 512;
        [SerializeField] private float _landPriceMultiplier = 1f;
        [SerializeField] private int _asteroidTier = 1;
        [SerializeField] private AsteroidDefinition[] _asteroidTypes;

        public string             PlanetId              => _planetId;
        public string             DisplayName           => _displayName;
        public GameObject         ModelPrefab           => _modelPrefab;
        public int                TileCount             => _tileCount;
        public float              LandPriceMultiplier   => _landPriceMultiplier;
        public int                AsteroidTier          => _asteroidTier;
        public AsteroidDefinition[] AsteroidTypes       => _asteroidTypes;
    }
}
