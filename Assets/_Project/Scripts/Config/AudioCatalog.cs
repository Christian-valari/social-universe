using UnityEngine;

namespace SocialUniverse.Config
{
    [System.Serializable]
    public struct SfxEntry
    {
        public SfxId     Id;
        public AudioClip Clip;
    }

    // Data-driven catalog of every clip AudioManager can play, aside from
    // per-planet BGM (which lives on PlanetDefinition.BgmClip itself).
    [CreateAssetMenu(menuName = "SocialUniverse/Config/AudioCatalog", fileName = "AudioCatalog")]
    public class AudioCatalog : ScriptableObject
    {
        [Header("BGM — non-planet scenes")]
        [SerializeField] private AudioClip _solarSystemBgm;
        [SerializeField] private AudioClip _travelBgm;
        [SerializeField] private AudioClip _fallbackPlanetBgm; // planets with no BgmClip of their own (Saturn, Pluto)

        [Header("SFX")]
        [SerializeField] private SfxEntry[] _sfxEntries;

        public AudioClip SolarSystemBgm    => _solarSystemBgm;
        public AudioClip TravelBgm         => _travelBgm;
        public AudioClip FallbackPlanetBgm => _fallbackPlanetBgm;

        public AudioClip GetSfxClip(SfxId id)
        {
            // Null on an instance built with CreateInstance (RootLifetimeScope's fallback when no
            // catalog is assigned), since Unity only initializes serialized arrays on loaded assets.
            if (_sfxEntries == null) return null;
            foreach (var entry in _sfxEntries)
                if (entry.Id == id) return entry.Clip;
            return null;
        }
    }
}
