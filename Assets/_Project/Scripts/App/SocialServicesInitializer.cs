using System;
using System.Threading.Tasks;
using SocialUniverse.Core;
using SocialUniverse.Net;
using SocialUniverse.Social;
using VContainer.Unity;

namespace SocialUniverse.App
{
    // Brings the social layer online once sign-in completes: connects chat
    // (Vivox login in production, instant in dev mode) and initializes the
    // friends roster. Channel join happens later, per planet
    // (PlanetPresenceController) — each planet is its own Vivox channel, so
    // there's no shared lobby channel to join here. Also syncs ServerTime here — it's registered as a singleton but
    // nothing previously called SyncAsync(), so every server-issued timestamp
    // (e.g. a trip's arrivalTs) was being compared against the unsynced local
    // device clock; any clock drift directly corrupted the Travel scene's
    // countdown (reported: a 2-minute trip showing ~15s left almost
    // immediately). Lives in the Root scope — social services and ServerTime
    // both span scenes.
    public class SocialServicesInitializer : IStartable, IDisposable
    {
        private readonly IChatService    _chat;
        private readonly IFriendsService _friends;
        private readonly IAuthService    _auth;
        private readonly ServerTime      _serverTime;
        private readonly ProfileService  _profile;

        // DirectMessageService subscribes to inbound DMs in its constructor;
        // depending on it here forces eager construction so DMs are captured
        // app-wide even before any chat UI resolves it.
        public SocialServicesInitializer(
            IChatService          chat,
            IFriendsService       friends,
            IAuthService          auth,
            ServerTime            serverTime,
            ProfileService        profile,
            DirectMessageService  _)
        {
            _chat       = chat;
            _friends    = friends;
            _auth       = auth;
            _serverTime = serverTime;
            _profile    = profile;
        }

        public void Start()
        {
            EventBus.Subscribe<PlayerReadyEvent>(OnPlayerReady);
            EventBus.Subscribe<PlayerLoggedOutEvent>(OnPlayerLoggedOut);
        }

        public void Dispose()
        {
            EventBus.Unsubscribe<PlayerReadyEvent>(OnPlayerReady);
            EventBus.Unsubscribe<PlayerLoggedOutEvent>(OnPlayerLoggedOut);
        }

        private async void OnPlayerReady(PlayerReadyEvent _)
        {
            // This name is baked into the Vivox login and shown to every other
            // player as the chat sender name (ChatMessageItemView), so the
            // resolver never falls back further than "Player" — falling through
            // to _auth.PlayerId would leak the player's raw UGS id into every
            // chat message. AuthService hydrates the UGS PlayerName cache before
            // the PlayerReadyEvent that triggers this, so DisplayName is the
            // real registered name here, not null (the pre-hydration bug that
            // baked "Player" into every post-registration session).
            string displayName = ChatDisplayNameResolver.Resolve(_auth.DisplayName, _auth.Username);

            // Kick off the real Vivox login immediately, synchronously, with the
            // best display name we already have - before awaiting anything below.
            // Otherwise a fast Auth -> Hub -> Planet transition can land
            // PlanetPresenceController's channel join first, which falls through
            // ChatService.EnsureConnectedAsync's own "Player" placeholder (its
            // last-resort guard against a login race) and bakes that into the
            // Vivox session as this player's name for good - visible to every
            // other player, not just this client.
            Task connectTask;
            try
            {
                connectTask = _chat.ConnectAsync(displayName, null);
            }
            catch (Exception ex)
            {
                SULog.Warn($"SocialServicesInitializer: chat connect failed ({ex.Message})", SULog.Channel.Social);
                connectTask = Task.CompletedTask;
            }

            // SyncAsync never throws (it logs and falls back to the local clock on
            // failure), so this needs no try/catch of its own.
            await _serverTime.SyncAsync();

            // Non-fatal: a failed profile fetch just means own messages fall back
            // to the placeholder avatar too, same as any other unresolved sender.
            string avatarId = null;
            try
            {
                var profile = await _profile.GetProfileAsync(_auth.PlayerId);
                avatarId = profile?.AvatarId;
                if (!string.IsNullOrEmpty(profile?.DisplayName))
                    displayName = profile.DisplayName;
            }
            catch (Exception ex)
            {
                SULog.Warn($"SocialServicesInitializer: profile fetch for avatar failed ({ex.Message})", SULog.Channel.Social);
            }

            try
            {
                await connectTask;
                // Re-stamps the resolved avatarId onto the already-connecting/
                // connected service; ConnectAsync is safe to call again (single-
                // flight via _connectTask, see ChatService/LocalMockChatService).
                await _chat.ConnectAsync(displayName, avatarId);
                SULog.Info("SocialServicesInitializer: chat connected", SULog.Channel.Social);
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

        private async void OnPlayerLoggedOut(PlayerLoggedOutEvent _)
        {
            try
            {
                await _chat.DisconnectAsync();
                SULog.Info("SocialServicesInitializer: chat disconnected on logout", SULog.Channel.Social);
            }
            catch (Exception ex)
            {
                SULog.Warn($"SocialServicesInitializer: chat disconnect failed ({ex.Message})", SULog.Channel.Social);
            }
        }
    }
}
