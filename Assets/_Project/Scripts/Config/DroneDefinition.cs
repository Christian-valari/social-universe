using UnityEngine;

namespace SocialUniverse.Config
{
    [CreateAssetMenu(menuName = "SocialUniverse/Config/DroneDefinition", fileName = "NewDrone")]
    public class DroneDefinition : ScriptableObject
    {
        [SerializeField] private string     _droneId;
        [SerializeField] private string     _displayName;
        [SerializeField] private float      _travelSpeed   = 5f;
        [SerializeField] private int        _cargoCap      = 50;
        [SerializeField] private GameObject _modelPrefab;

        public string     DroneId       => _droneId;
        public string     DisplayName   => _displayName;
        public float      TravelSpeed   => _travelSpeed;
        public int        CargoCap      => _cargoCap;
        public GameObject ModelPrefab   => _modelPrefab;
    }
}
