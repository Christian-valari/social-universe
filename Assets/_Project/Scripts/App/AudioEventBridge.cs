using VContainer.Unity;
using SocialUniverse.Core;
using SocialUniverse.Config;
using SocialUniverse.Safety;
using SocialUniverse.Social;

namespace SocialUniverse.App
{
    // Plays SFX in response to global EventBus events that have no single
    // natural DI-wired UI component to own the call — chat messages (any
    // scene) and rocket takeoff/landing (the TravelLoading scene, which has
    // no LifetimeScope of its own — see plan Task 5 for why). Lives in the
    // Root scope so it's alive for every scene these events can fire in,
    // same lifetime shape as SocialServicesInitializer.
    public class AudioEventBridge : IStartable, System.IDisposable
    {
        private readonly IAudioManager _audio;

        public AudioEventBridge(IAudioManager audio)
        {
            _audio = audio;
        }

        public void Start()
        {
            EventBus.Subscribe<ChatChannelController.ChatMessageReceivedEvent>(OnChatMessageReceived);
            EventBus.Subscribe<TravelLoadingTakeOffRequestedEvent>(OnTakeOffRequested);
            EventBus.Subscribe<TravelLoadingLandRequestedEvent>(OnLandRequested);
        }

        public void Dispose()
        {
            EventBus.Unsubscribe<ChatChannelController.ChatMessageReceivedEvent>(OnChatMessageReceived);
            EventBus.Unsubscribe<TravelLoadingTakeOffRequestedEvent>(OnTakeOffRequested);
            EventBus.Unsubscribe<TravelLoadingLandRequestedEvent>(OnLandRequested);
        }

        private void OnChatMessageReceived(ChatChannelController.ChatMessageReceivedEvent e)
        {
            if (e.Message.FromSelf) return; // no ping for your own outgoing message
            _audio.PlaySfx(SfxId.NewMessage);
        }

        private void OnTakeOffRequested(TravelLoadingTakeOffRequestedEvent e) => _audio.PlaySfx(SfxId.RocketDepart);
        private void OnLandRequested(TravelLoadingLandRequestedEvent e)       => _audio.PlaySfx(SfxId.RocketArrive);
    }
}
