using System;

namespace SocialUniverse.Safety
{
    public interface IAudioSettingsService
    {
        float MusicVolume01 { get; }
        float SfxVolume01   { get; }

        event Action<float> OnMusicVolumeChanged;
        event Action<float> OnSfxVolumeChanged;

        void SetMusicVolume(float value01);
        void SetSfxVolume(float value01);
    }
}
