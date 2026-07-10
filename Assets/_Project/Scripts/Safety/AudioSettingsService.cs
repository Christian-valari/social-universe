using System;
using SocialUniverse.Config;
using SocialUniverse.Core;
using UnityEngine;

namespace SocialUniverse.Safety
{
    // Local device audio preferences (music/SFX volume) — not server-authoritative,
    // so unlike IEconomyService/IAuthService there is no LocalMock/real split, just
    // this one implementation. Persists to PlayerPrefs and applies to AudioConfig's
    // mixer using the standard Unity linear-to-decibel slider conversion.
    public class AudioSettingsService : IAudioSettingsService
    {
        private readonly AudioConfig _config;
        private float _musicVolume01;
        private float _sfxVolume01;

        public float MusicVolume01 => _musicVolume01;
        public float SfxVolume01   => _sfxVolume01;

        public event Action<float> OnMusicVolumeChanged;
        public event Action<float> OnSfxVolumeChanged;

        public AudioSettingsService(AudioConfig config)
        {
            _config = config;
            _musicVolume01 = PlayerPrefs.GetFloat(SaveKeys.MusicVolume, 1f);
            _sfxVolume01   = PlayerPrefs.GetFloat(SaveKeys.SfxVolume, 1f);
            ApplyToMixer(_config.MusicVolumeParam, _musicVolume01);
            ApplyToMixer(_config.SfxVolumeParam, _sfxVolume01);
        }

        public void SetMusicVolume(float value01)
        {
            _musicVolume01 = Mathf.Clamp01(value01);
            PlayerPrefs.SetFloat(SaveKeys.MusicVolume, _musicVolume01);
            PlayerPrefs.Save();
            ApplyToMixer(_config.MusicVolumeParam, _musicVolume01);
            OnMusicVolumeChanged?.Invoke(_musicVolume01);
        }

        public void SetSfxVolume(float value01)
        {
            _sfxVolume01 = Mathf.Clamp01(value01);
            PlayerPrefs.SetFloat(SaveKeys.SfxVolume, _sfxVolume01);
            PlayerPrefs.Save();
            ApplyToMixer(_config.SfxVolumeParam, _sfxVolume01);
            OnSfxVolumeChanged?.Invoke(_sfxVolume01);
        }

        private void ApplyToMixer(string param, float value01)
        {
            // Mixer is optional at the service level (kept unassigned in unit
            // tests, guaranteed assigned in the real AudioConfig asset — see
            // Task 4). A missing mixer here just means volume still persists
            // as a preference without any audible effect yet.
            if (_config.Mixer == null) return;
            _config.Mixer.SetFloat(param, LinearToDecibel(value01));
        }

        public static float LinearToDecibel(float value01) =>
            value01 <= 0.0001f ? -80f : Mathf.Log10(value01) * 20f;
    }
}
