using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Net;
using SocialUniverse.Social;
using SocialUniverse.Tests;
using UnityEngine;

namespace SocialUniverse.Tests.Net
{
    // VivoxPresenceService delegates channel join/leave to ChatChannelController
    // (so chat-join and presence-join are the same Vivox channel) and is
    // constructed defensively when VivoxService.Instance isn't available (as in
    // this EditMode environment) — these tests cover that delegation without a
    // live Vivox session.
    public class VivoxPresenceServiceTests
    {
        private SocialConfig          _config;
        private FakeChatService       _chat;
        private FakeSocialBackendClient _backend;
        private ReportService         _reports;
        private ChatChannelController _channels;
        private VivoxPresenceService  _presence;

        [SetUp]
        public void SetUp()
        {
            _config   = ScriptableObject.CreateInstance<SocialConfig>();
            _chat     = new FakeChatService();
            _backend  = new FakeSocialBackendClient();
            _reports  = new ReportService(_backend, _chat);
            _channels = new ChatChannelController(_chat, new ChatModerationFilter(_config), _reports, _config);
            _presence = new VivoxPresenceService(_channels);
        }

        [TearDown]
        public void TearDown()
        {
            _presence.Dispose();
            _channels.Dispose();
            Object.DestroyImmediate(_config);
            EventBus.Clear();
        }

        [Test]
        public void Not_connected_before_joining_a_planet()
        {
            Assert.IsFalse(_presence.IsConnected);
            Assert.IsNull(_presence.CurrentChannelName);
        }

        [Test]
        public async Task JoinPlanetAsync_joins_that_planets_own_channel()
        {
            // Each planet is its own Vivox channel, so presence (and chat) for
            // Planet_Earth is isolated from every other planet's channel.
            bool joined = await _presence.JoinPlanetAsync("Planet_Earth");

            string expectedChannel = ChatChannelController.PlanetChannelName("Planet_Earth");
            Assert.IsTrue(joined);
            Assert.IsTrue(_presence.IsConnected);
            Assert.AreEqual(expectedChannel, _presence.CurrentChannelName);
            Assert.AreEqual(_channels.ActiveChannel, _presence.CurrentChannelName);
            Assert.Contains(expectedChannel, _chat.JoinedChannels);
        }

        [Test]
        public async Task JoinPlanetAsync_for_different_planets_uses_different_channels()
        {
            await _presence.JoinPlanetAsync("Planet_Earth");
            string earthChannel = _presence.CurrentChannelName;

            await _presence.JoinPlanetAsync("Planet_Mars");

            Assert.AreNotEqual(earthChannel, _presence.CurrentChannelName);
            Assert.AreEqual(ChatChannelController.PlanetChannelName("Planet_Mars"), _presence.CurrentChannelName);
        }

        [Test]
        public async Task LeaveAsync_leaves_the_planet_channel_entirely()
        {
            // Leaving presence disconnects chat too — each planet's channel is
            // exclusive to that planet, so there's no shared fallback channel.
            await _presence.JoinPlanetAsync("Planet_Earth");

            await _presence.LeaveAsync();

            Assert.IsNull(_presence.CurrentChannelName);
            Assert.IsFalse(_presence.IsConnected);
        }

        [Test]
        public void Players_is_empty_without_a_live_Vivox_session()
        {
            // No VivoxService.Instance in this EditMode environment — the
            // service must degrade to an empty roster rather than throw.
            Assert.IsEmpty(_presence.Players);
        }
    }
}
