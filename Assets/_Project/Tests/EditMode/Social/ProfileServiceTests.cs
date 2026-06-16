using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Social;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class ProfileServiceTests
    {
        private SocialConfig            _config;
        private FakeSocialBackendClient _backend;
        private ProfileService          _profiles;

        [SetUp]
        public void SetUp()
        {
            _config   = ScriptableObject.CreateInstance<SocialConfig>();
            _backend  = new FakeSocialBackendClient();
            _profiles = new ProfileService(_backend, _config, new ChatModerationFilter(_config));
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        [Test]
        public async Task GetProfileAsync_calls_GetPlayerProfile_with_playerId()
        {
            _backend.ProfileResponse = new PlayerProfile
            {
                PlayerId = "p1", DisplayName = "Stella", Level = 7, TilesOwned = 3
            };

            var profile = await _profiles.GetProfileAsync("p1");

            Assert.AreEqual("GetPlayerProfile", _backend.CalledFunction);
            Assert.AreEqual("p1", _backend.CalledArgs["playerId"]);
            Assert.AreEqual("Stella", profile.DisplayName);
            Assert.AreEqual(7, profile.Level);
            Assert.AreEqual(3, profile.TilesOwned);
        }

        [Test]
        public async Task Empty_display_name_is_rejected_without_server_call()
        {
            var result = await _profiles.UpdateDisplayNameAsync("   ");

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NAME_EMPTY", result.Reason);
            Assert.IsNull(_backend.CalledFunction);
        }

        [Test]
        public async Task Too_long_display_name_is_rejected_without_server_call()
        {
            var result = await _profiles.UpdateDisplayNameAsync(new string('x', _config.MaxDisplayNameLength + 1));

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NAME_TOO_LONG", result.Reason);
            Assert.IsNull(_backend.CalledFunction);
        }

        [Test]
        public async Task Profane_display_name_is_rejected_without_server_call()
        {
            var result = await _profiles.UpdateDisplayNameAsync("xX_sh1t_Xx");

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NAME_REJECTED", result.Reason);
            Assert.IsNull(_backend.CalledFunction);
        }

        [Test]
        public async Task Valid_display_name_is_committed_via_UpdateProfile()
        {
            _backend.ProfileUpdateResponse = new ProfileUpdateResult { Success = true, DisplayName = "Stella" };

            var result = await _profiles.UpdateDisplayNameAsync("  Stella  ");

            Assert.AreEqual("UpdateProfile", _backend.CalledFunction);
            Assert.AreEqual("Stella", _backend.CalledArgs["displayName"]); // trimmed
            Assert.IsTrue(result.Success);
        }
    }
}
