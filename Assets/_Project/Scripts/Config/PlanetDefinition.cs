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
        [SerializeField] private int _asteroidFieldSize = 6; // total asteroids simultaneously present on this planet
        [SerializeField] private AsteroidDefinition[] _asteroidTypes;
        [SerializeField] private int _travelFuelCost = 20; // fuel spent to travel here from the Hub; ignored for the home planet (free trip home)
        [SerializeField] private float _travelDurationSeconds = 30f; // fallback time-in-transit when traveling here, used only if the origin/destination pair isn't in TravelTimeTable (e.g. Pluto, which Data/Travel_Times.csv doesn't cover). Must be kept in sync with ServerCode/StartTravel.js's PLUTO_FALLBACK_SEC.
        [SerializeField] private int _orbitOrder = 0;      // display/adjacency order on the star map (ascending = outward from the sun)
        [SerializeField] private float _orbitDistanceAU = 1f; // approximate real distance from the sun in AU, used only to lay planets out at relatively-correct distances in Sky Discovery

        public string             PlanetId              => _planetId;
        public string             DisplayName           => _displayName;
        public GameObject         ModelPrefab           => _modelPrefab;
        public int                TileCount             => _tileCount;
        public float              LandPriceMultiplier   => _landPriceMultiplier;
        public int                AsteroidTier          => _asteroidTier;
        public int                AsteroidFieldSize     => _asteroidFieldSize;
        public AsteroidDefinition[] AsteroidTypes       => _asteroidTypes;
        public int                TravelFuelCost        => _travelFuelCost;
        public float              TravelDurationSeconds => _travelDurationSeconds;
        public int                OrbitOrder            => _orbitOrder;
        public float              OrbitDistanceAU       => _orbitDistanceAU;
    }
}
