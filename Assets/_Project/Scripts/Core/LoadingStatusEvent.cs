namespace SocialUniverse.Core
{
    // Published by PlanetSceneBootstrapper to report loading progress (0 = start, 1 = complete).
    public readonly struct LoadingStatusEvent
    {
        public readonly float Progress;
        public LoadingStatusEvent(float progress) => Progress = progress;
    }
}
