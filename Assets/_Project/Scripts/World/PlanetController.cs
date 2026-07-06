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
            var model = Instantiate(planet.ModelPrefab, transform.position, Quaternion.identity, transform);

            // ModelPrefab assets carry a SphereCollider for their star-map/travel-preview uses
            // (tap-to-select, preview rotation). Here the model is purely decorative and is
            // parented at the same position/scale as the Hexasphere's own tile-interaction
            // SphereCollider, so the model's collider silently wins every raycast — both Unity's
            // OnMouseEnter/Exit and the plugin's own hit-test (which requires
            // hit.collider.gameObject == the Hexasphere's GameObject) — and hex tiles never
            // register hover or clicks.
            foreach (var col in model.GetComponentsInChildren<Collider>())
                col.enabled = false;
        }
    }
}
