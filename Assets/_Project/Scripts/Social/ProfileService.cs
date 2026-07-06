using System.Collections.Generic;
using System.Threading.Tasks;
using SocialUniverse.Config;
using SocialUniverse.Core;

namespace SocialUniverse.Social
{
    // Response shape for the "UpdateProfile" Cloud Code function. Public so
    // tests can construct it for a fake IBackendClient.
    public class ProfileUpdateResult
    {
        public bool   Success;
        public string Reason;       // e.g. "NAME_REJECTED", "NAME_TOO_LONG"
        public string DisplayName;  // the (possibly server-normalized) committed name
    }

    // Fetch/update player profiles. Reads go through "GetPlayerProfile" so any
    // player's public profile is visible; writes go through "UpdateProfile",
    // which re-validates the display name server-side (the client-side check
    // here is just fast feedback).
    public class ProfileService
    {
        private readonly IBackendClient       _backend;
        private readonly SocialConfig         _config;
        private readonly ChatModerationFilter _filter;

        public ProfileService(IBackendClient backend, SocialConfig config, ChatModerationFilter filter)
        {
            _backend = backend;
            _config  = config;
            _filter  = filter;
        }

        public Task<PlayerProfile> GetProfileAsync(string playerId) =>
            _backend.CallAsync<PlayerProfile>("GetPlayerProfile",
                new Dictionary<string, object> { { "playerId", playerId } });

        public async Task<ProfileUpdateResult> UpdateDisplayNameAsync(string displayName)
        {
            displayName = displayName?.Trim();

            if (string.IsNullOrEmpty(displayName))
                return new ProfileUpdateResult { Success = false, Reason = "NAME_EMPTY" };
            if (displayName.Length > _config.MaxDisplayNameLength)
                return new ProfileUpdateResult { Success = false, Reason = "NAME_TOO_LONG" };
            if (!_filter.IsClean(displayName))
                return new ProfileUpdateResult { Success = false, Reason = "NAME_REJECTED" };

            return await _backend.CallAsync<ProfileUpdateResult>("UpdateProfile",
                new Dictionary<string, object> { { "displayName", displayName } });
        }

        public Task<ProfileUpdateResult> UpdateAvatarAsync(string avatarId) =>
            _backend.CallAsync<ProfileUpdateResult>("UpdateProfile",
                new Dictionary<string, object> { { "avatarId", avatarId } });
    }
}
