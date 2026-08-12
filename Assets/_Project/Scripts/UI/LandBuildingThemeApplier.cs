using UnityEngine;
using UnityEngine.Rendering;
using VContainer;
using SocialUniverse.Config;
using SocialUniverse.Core;

namespace SocialUniverse.UI
{
    // Applies the active planet's LandBuilding theme to the scene sky + ambient at load.
    // Sky: swaps the theme's texture onto the SkyDome renderers via ONE runtime material
    // instance, so the shared SimpleSky.mat asset is never mutated and all dome faces stay
    // in sync. Ambient: sets global RenderSettings (the Planet scene re-establishes its own
    // lighting on return, so this doesn't visually leak). Hex materials are handled separately
    // inside PlotHexBoard. Registered via RegisterComponentInHierarchy in LandBuildingSceneScope.
    public class LandBuildingThemeApplier : MonoBehaviour
    {
        [SerializeField] private Renderer[] _skyRenderers;   // the SimpleSky SkyDome faces
        [SerializeField] private LandBuildingThemeDefinition _defaultTheme; // fallback (standalone / no-theme)

        [Inject] private LandBuildingHandoff _handoff;
        [Inject] private DatabaseRegistry    _registry;

        private Material _skyInstance; // runtime clone we own; destroyed with this component so it doesn't orphan per scene entry

        private void Start()
        {
            var theme = LandBuildingThemeResolver.Resolve(
                _registry, _handoff != null ? _handoff.PlanetId : null, _defaultTheme);
            if (theme == null) return;

            ApplySky(theme.SkyTexture);
            ApplyAmbient(theme.AmbientColor, theme.AmbientIntensity);
        }

        private void ApplySky(Texture2D skyTexture)
        {
            if (skyTexture == null || _skyRenderers == null) return;

            foreach (var r in _skyRenderers)
            {
                if (r == null || r.sharedMaterial == null) continue; // need a source material to clone from
                if (_skyInstance == null)
                {
                    _skyInstance = new Material(r.sharedMaterial);
                    _skyInstance.mainTexture = skyTexture;
                }
                r.sharedMaterial = _skyInstance;
            }
        }

        // The sky instance is a runtime Material we allocated and assigned via sharedMaterial, so Unity
        // won't auto-destroy it on scene unload — free it here to avoid leaking one per LandBuilding entry.
        private void OnDestroy()
        {
            if (_skyInstance != null) Destroy(_skyInstance);
        }

        private void ApplyAmbient(Color color, float intensity)
        {
            RenderSettings.ambientMode      = AmbientMode.Flat;
            RenderSettings.ambientLight     = color;
            RenderSettings.ambientIntensity = intensity;
        }
    }
}
