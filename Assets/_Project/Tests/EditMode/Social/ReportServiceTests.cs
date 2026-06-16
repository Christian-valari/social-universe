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
    }
}
