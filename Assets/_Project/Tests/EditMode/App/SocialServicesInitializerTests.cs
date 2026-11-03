using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.App;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Net;
using SocialUniverse.Social;
using UnityEngine;

namespace SocialUniverse.Tests
{
    // Sign-in pulls the block list from the server; sign-out forgets it (and mutes).
    public class SocialServicesInitializerTests
    {
        private BlockListSyncTests.ScriptedCloudSave _cloudSave;
        private ReportService            _reports;
        private SocialServicesInitializer _initializer;
        private SocialConfig             _config;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            _config = ScriptableObject.CreateInstance<SocialConfig>();
            var backend = new FakeSocialBackendClient();
            var chat    = new FakeChatService();
            var filter  = new ChatModerationFilter(_config);
            var friends = new LocalMockFriendsService();

            _cloudSave = new BlockListSyncTests.ScriptedCloudSave();
            _reports   = new ReportService(backend, chat);
            _initializer = new SocialServicesInitializer(
                chat, friends, new LocalMockAuthService(), new ServerTime(backend),
                new ProfileService(backend, _config, filter),
                new DirectMessageService(chat, friends, filter, _reports, _config),
                _reports, new BlockListSync(_cloudSave, _reports));
            _initializer.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _initializer.Dispose();
            Object.DestroyImmediate(_config);
            EventBus.Clear();
        }

        [Test]
        public async Task Sign_in_pulls_the_block_list_from_the_server()
        {
            _cloudSave.Records[SaveKeys.BlockedUsers] = new List<string> { "griefer_42" };

            EventBus.Publish(new PlayerReadyEvent());
            await Task.Yield();

            Assert.IsTrue(_reports.HasBlockList);
            Assert.IsTrue(_reports.IsBlocked("griefer_42"));
        }

        [Test]
        public async Task Sign_out_forgets_the_players_blocks_and_mutes()
        {
            _reports.SetBlockList(new[] { "griefer_42" });
            _reports.MutePlayer("loud_guy", true);

            EventBus.Publish(new PlayerLoggedOutEvent());
            await Task.Yield();

            Assert.IsFalse(_reports.HasBlockList);
            Assert.IsFalse(_reports.IsBlocked("griefer_42"));
            Assert.IsFalse(_reports.IsMuted("loud_guy"));
        }
    }
}
