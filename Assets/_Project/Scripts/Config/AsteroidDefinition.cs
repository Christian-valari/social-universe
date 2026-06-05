using UnityEngine;

namespace SocialUniverse.Config
{
    [CreateAssetMenu(menuName = "SocialUniverse/Config/AsteroidDefinition", fileName = "NewAsteroid")]
    public class AsteroidDefinition : ScriptableObject
    {
        [SerializeField] private string     _mineralType;
        [SerializeField] private int        _tier          = 1;
        [SerializeField] private int        _baseYield     = 50;
        [SerializeField] [Range(0f, 1f)]
                         private float      _rarity        = 0.5f;
        [SerializeField] private int        _coinsPerUnit  = 2;
        [SerializeField] private GameObject _modelPrefab;

        public string     MineralType   => _mineralType;
        public int        Tier          => _tier;
        public int        BaseYield     => _baseYield;
        public float      Rarity        => _rarity;
        public int        CoinsPerUnit  => _coinsPerUnit;
        public GameObject ModelPrefab   => _modelPrefab;
    }
}
