using System;
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

        public void Start() => EventBus.Subscribe<PlayerReadyEvent>(OnPlayerReady);

        public void Dispose() => EventBus.Unsubscribe<PlayerReadyEvent>(OnPlayerReady);

        private async void OnPlayerReady(PlayerReadyEvent _)
        {
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
            }
            catch (Exception ex)
            {
                SULog.Warn($"SocialServicesInitializer: profile fetch for avatar failed ({ex.Message})", SULog.Channel.Social);
            }

            try
            {
                await _chat.ConnectAsync(_auth.DisplayName ?? _auth.Username ?? _auth.PlayerId, avatarId);
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
    }
}
