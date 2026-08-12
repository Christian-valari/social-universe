using UnityEngine;

namespace SocialUniverse.Config
{
    // One planet's LandBuilding look. Referenced by PlanetDefinition._landBuildingTheme and
    // resolved at scene load by LandBuildingThemeResolver. Any field left null/default means
    // "use the scene fallback for this aspect" — a partially-authored theme is valid.
    [CreateAssetMenu(menuName = "SocialUniverse/Config/LandBuildingTheme", fileName = "NewLandBuildingTheme")]
    public class LandBuildingThemeDefinition : ScriptableObject
    {
        [SerializeField] private Texture2D _skyTexture;            // swapped onto the SkyDome
        [SerializeField] private Material  _hexLockedMaterial;     // locked hexatile look
        [SerializeField] private Material  _hexUnlockedMaterial;   // unlocked hexatile look
        [SerializeField] private Color     _ambientColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        [SerializeField] private float     _ambientIntensity = 1f;

        public Texture2D SkyTexture          => _skyTexture;
        public Material  HexLockedMaterial   => _hexLockedMaterial;
        public Material  HexUnlockedMaterial => _hexUnlockedMaterial;
        public Color     AmbientColor        => _ambientColor;
        public float     AmbientIntensity    => _ambientIntensity;
    }
}
