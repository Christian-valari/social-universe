using System;
using VContainer.Unity;
using SocialUniverse.Core;

namespace SocialUniverse.App
{
    public class ActiveMiningRequestHandler : IStartable, IDisposable
    {
        private readonly PlanetState _planetState;

        public ActiveMiningRequestHandler(PlanetState planetState) => _planetState = planetState;

        public void Start()   => EventBus.Subscribe<ActiveMiningRequestedEvent>(OnActiveMiningRequested);
        public void Dispose() => EventBus.Unsubscribe<ActiveMiningRequestedEvent>(OnActiveMiningRequested);

        private void OnActiveMiningRequested(ActiveMiningRequestedEvent e) => _planetState.EnterActiveMining();
    }
}
