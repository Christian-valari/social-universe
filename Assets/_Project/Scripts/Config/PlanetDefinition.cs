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
        [SerializeField] private int _travelFuelCost = 20; // fuel spent to travel here from the Hub; ignored for the home planet (free trip home)
        [SerializeField] private int _orbitOrder = 0;      // display/adjacency order on the star map (ascending = outward from the sun); travel range is gated by how many steps apart two planets' OrbitOrder is, see TravelRangeMath
        [SerializeField] private float _orbitDistanceAU = 1f; // approximate real distance from the sun in AU, used only to lay planets out at relatively-correct distances in Sky Discovery

        public string             PlanetId              => _planetId;
        public string             DisplayName           => _displayName;
        public GameObject         ModelPrefab           => _modelPrefab;
        public int                TileCount             => _tileCount;
        public float              LandPriceMultiplier   => _landPriceMultiplier;
        public int                AsteroidTier          => _asteroidTier;
        public AsteroidDefinition[] AsteroidTypes       => _asteroidTypes;
        public int                TravelFuelCost        => _travelFuelCost;
        public int                OrbitOrder            => _orbitOrder;
        public float              OrbitDistanceAU       => _orbitDistanceAU;
    }
}
