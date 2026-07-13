using SocialUniverse.Config;

namespace SocialUniverse.Safety
{
    public interface IAudioManager
    {
        void PlaySfx(SfxId id);
        void PlayBgmForPlanet(PlanetDefinition planet);
        void PlaySolarSystemBgm();
        void PlayTravelBgm();
    }
}
