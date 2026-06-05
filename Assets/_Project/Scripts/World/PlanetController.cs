using UnityEngine;
using VContainer;
using SocialUniverse.Config;
using SocialUniverse.Core;

namespace SocialUniverse.World
{
    public class PlanetLoadedEvent { public PlanetDefinition Planet; }

    public class PlanetController : MonoBehaviour
    {
        [SerializeField] private HexasphereManager _hexasphere;
        [SerializeField] private TileColorizer     _colorizer;

        [Inject] private LandmarkService _landmarkService;

        public PlanetDefinition CurrentPlanet { get; private set; }

        public void Load(PlanetDefinition planet)
        {
            CurrentPlanet = planet;
            SpawnModel(planet);
            _hexasphere.Generate(planet.TileCount);
            _landmarkService.MarkLandmarks(_hexasphere);
            _colorizer.Refresh(_hexasphere.Tiles);
            EventBus.Publish(new PlanetLoadedEvent { Planet = planet });
            SULog.Info($"PlanetController: loaded '{planet.DisplayName}'", SULog.Channel.World);
        }

        private void SpawnModel(PlanetDefinition planet)
        {
            if (planet.ModelPrefab == null) return;
            Instantiate(planet.ModelPrefab, transform.position, Quaternion.identity, transform);
        }
    }
}
