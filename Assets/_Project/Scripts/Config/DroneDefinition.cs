using UnityEngine;

namespace SocialUniverse.Config
{
    [CreateAssetMenu(menuName = "SocialUniverse/Config/DroneDefinition", fileName = "NewDrone")]
    public class DroneDefinition : ScriptableObject
    {
        [SerializeField] private string     _droneId;
        [SerializeField] private string     _displayName;
        [SerializeField] private int        _tier            = 1;   // highest asteroid tier this drone can mine
        [SerializeField] private int        _unlockCost      = 0;   // coins to acquire into the fleet (0 = starter)
        [SerializeField] private float      _travelSpeed     = 5f;  // base value, scaled by Speed upgrades
        [SerializeField] private int        _cargoCap        = 50;  // base value, scaled by Cargo upgrades
        [SerializeField] private float      _yieldMultiplier = 1f;  // base value, scaled by Yield upgrades
        [SerializeField] private GameObject _modelPrefab;

        public string     DroneId         => _droneId;
        public string     DisplayName     => _displayName;
        public int        Tier            => _tier;
        public int        UnlockCost      => _unlockCost;
        public float      TravelSpeed     => _travelSpeed;
        public int        CargoCap        => _cargoCap;
        public float      YieldMultiplier => _yieldMultiplier;
        public GameObject ModelPrefab     => _modelPrefab;
    }
}
