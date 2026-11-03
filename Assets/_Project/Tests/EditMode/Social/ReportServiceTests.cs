using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Social;

namespace SocialUniverse.Tests
{
    public class ReportServiceTests
    {
        private FakeSocialBackendClient _backend;
        private FakeChatService         _chat;
        private ReportService           _reports;

        [SetUp]
        public void SetUp()
        {
            _backend = new FakeSocialBackendClient();
            _chat    = new FakeChatService();
            _reports = new ReportService(_backend, _chat);
        }

        [Test]
        public async Task ReportPlayerAsync_calls_SubmitReport_with_payload()
        {
            _backend.ReportResponse = new ReportResult { Success = true, ReportId = "r1" };

            var result = await _reports.ReportPlayerAsync("griefer_42", "harassment", "said a bad thing");

            Assert.AreEqual("SubmitReport", _backend.CalledFunction);
            Assert.AreEqual("griefer_42", _backend.CalledArgs["targetId"]);
            Assert.AreEqual("harassment", _backend.CalledArgs["reason"]);
            Assert.AreEqual("said a bad thing", _backend.CalledArgs["context"]);
            Assert.IsTrue(result.Success);
            Assert.AreEqual("r1", result.ReportId);
        }

        [Test]
        public async Task BlockPlayerAsync_success_blocks_locally_and_at_chat_level()
        {
            _backend.BlockResponse = new BlockResult { Success = true, BlockedUsers = new[] { "griefer_42" } };

            var result = await _reports.BlockPlayerAsync("griefer_42");

            Assert.IsTrue(result.Success);
            Assert.AreEqual("BlockUser", _backend.CalledFunction);
            Assert.AreEqual(true, _backend.CalledArgs["blocked"]);
            Assert.IsTrue(_reports.IsBlocked("griefer_42"));
            Assert.Contains("griefer_42", _chat.BlockedPlayers);
        }

        [Test]
        public async Task BlockPlayerAsync_failure_does_not_block()
        {
            _backend.BlockResponse = new BlockResult { Success = false };

            await _reports.BlockPlayerAsync("griefer_42");

            Assert.IsFalse(_reports.IsBlocked("griefer_42"));
            Assert.IsEmpty(_chat.BlockedPlayers);
        }

        [Test]
        public async Task UnblockPlayerAsync_removes_block()
        {
            _backend.BlockResponse = new BlockResult { Success = true, BlockedUsers = new[] { "griefer_42" } };
            await _reports.BlockPlayerAsync("griefer_42");

            _backend.BlockResponse = new BlockResult { Success = true, BlockedUsers = new string[0] };
            await _reports.UnblockPlayerAsync("griefer_42");

            Assert.IsFalse(_reports.IsBlocked("griefer_42"));
            Assert.IsEmpty(_chat.BlockedPlayers);
        }

        [Test]
        public void MutePlayer_is_local_only()
        {
            _reports.MutePlayer("loud_guy", true);

            Assert.IsTrue(_reports.IsMuted("loud_guy"));
            Assert.IsNull(_backend.CalledFunction); // no server call for mutes

            _reports.MutePlayer("loud_guy", false);
            Assert.IsFalse(_reports.IsMuted("loud_guy"));
        }

        // The server's block list (Cloud Save "blocked_users") is pulled after sign-in, so a block made
        // on another device — or before the app restarted — filters chat here too.
        [Test]
        public void SetBlockList_replaces_the_cached_list_with_the_servers()
        {
            _reports.SetBlockList(new[] { "old_block" });

            _reports.SetBlockList(new[] { "griefer_42", "spammer_7" });

            Assert.IsTrue(_reports.HasBlockList);
            Assert.IsTrue(_reports.IsBlocked("griefer_42"));
            Assert.IsTrue(_reports.IsBlocked("spammer_7"));
            Assert.IsFalse(_reports.IsBlocked("old_block"));
        }

        [Test]
        public void HasBlockList_is_false_until_the_servers_list_is_pulled()
        {
            Assert.IsFalse(_reports.HasBlockList);

            _reports.SetBlockList(new string[0]);

            Assert.IsTrue(_reports.HasBlockList);
        }

        // ReportService outlives a sign-out (it is app-wide), so the next account on this device must
        // not inherit the previous one's blocks or mutes.
        [Test]
        public void Reset_forgets_the_signed_out_players_blocks_and_mutes()
        {
            _reports.SetBlockList(new[] { "griefer_42" });
            _reports.MutePlayer("loud_guy", true);

            _reports.Reset();

            Assert.IsFalse(_reports.IsBlocked("griefer_42"));
            Assert.IsFalse(_reports.IsMuted("loud_guy"));
            Assert.IsFalse(_reports.HasBlockList);
        }
    }
}
