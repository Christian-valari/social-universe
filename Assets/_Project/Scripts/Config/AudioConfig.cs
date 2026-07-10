using UnityEngine;
using UnityEngine.Audio;

namespace SocialUniverse.Config
{
    // Audio mixer wiring for the Settings panel's volume sliders. Kept as a
    // data asset (Architecture Rule 3) rather than hardcoded on the service
    // so the mixer/parameter names are inspector-editable and swappable
    // without touching code.
    [CreateAssetMenu(menuName = "SocialUniverse/Config/AudioConfig", fileName = "AudioConfig")]
    public class AudioConfig : ScriptableObject
    {
        [SerializeField] private AudioMixer _mixer;
        [SerializeField] private string _musicVolumeParam = "MusicVolume";
        [SerializeField] private string _sfxVolumeParam   = "SFXVolume";

        public AudioMixer Mixer          => _mixer;
        public string MusicVolumeParam   => _musicVolumeParam;
        public string SfxVolumeParam     => _sfxVolumeParam;
    }
}
