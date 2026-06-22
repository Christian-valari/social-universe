using System;
using VContainer.Unity;
using SocialUniverse.Core;

namespace SocialUniverse.App
{
    public class LaunchButtonHandler : IStartable, IDisposable
    {
        private readonly PlanetState _planetState;

        public LaunchButtonHandler(PlanetState planetState) => _planetState = planetState;

        public void Start()   => EventBus.Subscribe<LaunchRequestedEvent>(OnLaunchRequested);
        public void Dispose() => EventBus.Unsubscribe<LaunchRequestedEvent>(OnLaunchRequested);

        private void OnLaunchRequested(LaunchRequestedEvent e) => _planetState.ReturnToHub();
    }
}
