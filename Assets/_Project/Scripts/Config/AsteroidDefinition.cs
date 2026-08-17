using UnityEngine;

namespace SocialUniverse.Config
{
    [CreateAssetMenu(menuName = "SocialUniverse/Config/AsteroidDefinition", fileName = "NewAsteroid")]
    public class AsteroidDefinition : ScriptableObject
    {
        [SerializeField] private MineralDefinition _mineral;   // M6: authoritative mineral (drives inventory grants)
        [SerializeField] private string     _mineralType;      // retained: display label + AsteroidSpawner identity/persistence key
        [SerializeField] private int        _tier          = 1;
        [SerializeField] private int        _baseYield     = 50;
        [SerializeField] [Range(0f, 1f)]
                         private float      _rarity        = 0.5f;
        [SerializeField] private int        _coinsPerUnit  = 2; // retained (legacy); dead after Task 8, harmless
        [SerializeField] private GameObject _modelPrefab;

        public MineralDefinition Mineral       => _mineral;
        public string            MineralType   => _mineralType;
        public int               Tier          => _tier;
        public int               BaseYield     => _baseYield;
        public float             Rarity        => _rarity;
        public int               CoinsPerUnit  => _coinsPerUnit;
        public GameObject        ModelPrefab   => _modelPrefab;
    }
}
