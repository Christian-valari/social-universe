using System;
using VContainer.Unity;
using SocialUniverse.Core;
using SocialUniverse.Mining;

namespace SocialUniverse.App
{
    // Turns a server-rejected mining claim into the failure toast (ServerActionFailedEvent), so
    // Mining stays free of UI text. MiningController already logs the rejection.
    public class MiningClaimFeedbackHandler : IStartable, IDisposable
    {
        public void Start()   => EventBus.Subscribe<MiningClaimRejectedEvent>(OnRejected);
        public void Dispose() => EventBus.Unsubscribe<MiningClaimRejectedEvent>(OnRejected);

        private static void OnRejected(MiningClaimRejectedEvent e) =>
            EventBus.Publish(new ServerActionFailedEvent(ServerFailureMessages.For("Mining", e.Reason)));
    }
}
