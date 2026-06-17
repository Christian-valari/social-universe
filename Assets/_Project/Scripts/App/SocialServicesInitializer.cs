using System;
using SocialUniverse.Core;
using SocialUniverse.Social;
using VContainer.Unity;

namespace SocialUniverse.App
{
    // Brings the social layer online once sign-in completes: connects chat
    // (Vivox login in production, instant in dev mode), initializes the
    // friends roster, and joins the global channel so chat works from the Hub
    // onward. Lives in the Root scope — social services span scenes.
    public class SocialServicesInitializer : IStartable, IDisposable
    {
        private readonly IChatService          _chat;
        private readonly IFriendsService       _friends;
        private readonly ChatChannelController _channels;
        private readonly IAuthService          _auth;

        // DirectMessageService subscribes to inbound DMs in its constructor;
        // depending on it here forces eager construction so DMs are captured
        // app-wide even before any chat UI resolves it.
        public SocialServicesInitializer(
            IChatService          chat,
            IFriendsService       friends,
            ChatChannelController channels,
            IAuthService          auth,
            DirectMessageService  _)
        {
            _chat     = chat;
            _friends  = friends;
            _channels = channels;
            _auth     = auth;
        }

        public void Start() => EventBus.Subscribe<PlayerReadyEvent>(OnPlayerReady);

        public void Dispose() => EventBus.Unsubscribe<PlayerReadyEvent>(OnPlayerReady);

        private async void OnPlayerReady(PlayerReadyEvent _)
        {
            try
            {
                await _chat.ConnectAsync(_auth.DisplayName ?? _auth.Username ?? _auth.PlayerId);
                await _channels.SwitchToGlobalAsync();
                SULog.Info("SocialServicesInitializer: chat connected, global channel joined", SULog.Channel.Social);
            }
            catch (Exception ex)
            {
                SULog.Warn($"SocialServicesInitializer: chat connect failed ({ex.Message})", SULog.Channel.Social);
            }

            try
            {
                await _friends.InitializeAsync();
            }
            catch (Exception ex)
            {
                SULog.Warn($"SocialServicesInitializer: friends init failed ({ex.Message})", SULog.Channel.Social);
            }
        }
    }
}
