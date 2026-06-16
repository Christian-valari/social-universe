using System.Collections.Generic;
using System.Threading.Tasks;
using SocialUniverse.Core;

namespace SocialUniverse.Social
{
    // Response shape for the "SubmitReport" Cloud Code function. Public so
    // tests can construct it for a fake IBackendClient.
    public class ReportResult
    {
        public bool   Success;
        public string ReportId;
    }

    // Response shape for the "BlockUser" Cloud Code function. The server
    // returns the caller's full block list so the client cache converges even
    // if a previous call was lost.
    public class BlockResult
    {
        public bool     Success;
        public string[] BlockedUsers;
    }

    // Report / block / mute. Reports and blocks always route through
    // ServerCode (SubmitReport / BlockUser) — the client cannot self-moderate;
    // it only caches the result. Mute is intentionally local-only (a personal
    // display preference, not a moderation action).
    public class ReportService
    {
        private readonly IBackendClient  _backend;
        private readonly IChatService    _chat;
        private readonly HashSet<string> _blocked = new();
        private readonly HashSet<string> _muted   = new();

        public ReportService(IBackendClient backend, IChatService chat)
        {
            _backend = backend;
            _chat    = chat;
        }

        public bool IsBlocked(string playerId) => playerId != null && _blocked.Contains(playerId);
        public bool IsMuted(string playerId)   => playerId != null && _muted.Contains(playerId);

        public Task<ReportResult> ReportPlayerAsync(string targetId, string reason, string contextText = null) =>
            _backend.CallAsync<ReportResult>("SubmitReport", new Dictionary<string, object>
            {
                { "targetId", targetId },
                { "reason",   reason },
                { "context",  contextText ?? "" }
            });

        public async Task<BlockResult> BlockPlayerAsync(string targetId)
        {
            var result = await _backend.CallAsync<BlockResult>("BlockUser", new Dictionary<string, object>
            {
                { "targetId", targetId },
                { "blocked",  true }
            });

            if (result is { Success: true })
            {
                ApplyServerBlockList(result.BlockedUsers, ensure: targetId);
                await TryChatBlockAsync(targetId, blocked: true);
            }
            return result;
        }

        public async Task<BlockResult> UnblockPlayerAsync(string targetId)
        {
            var result = await _backend.CallAsync<BlockResult>("BlockUser", new Dictionary<string, object>
            {
                { "targetId", targetId },
                { "blocked",  false }
            });

            if (result is { Success: true })
            {
                _blocked.Remove(targetId);
                ApplyServerBlockList(result.BlockedUsers, ensure: null);
                await TryChatBlockAsync(targetId, blocked: false);
            }
            return result;
        }

        public void MutePlayer(string targetId, bool muted)
        {
            if (targetId == null) return;
            if (muted) _muted.Add(targetId);
            else       _muted.Remove(targetId);
        }

        private void ApplyServerBlockList(string[] serverList, string ensure)
        {
            if (serverList != null)
            {
                _blocked.Clear();
                foreach (var id in serverList) _blocked.Add(id);
            }
            if (ensure != null) _blocked.Add(ensure);
        }

        private async Task TryChatBlockAsync(string targetId, bool blocked)
        {
            // Provider-level block is best-effort: in dev mode or before chat
            // connects the local block list above still applies.
            try
            {
                if (blocked) await _chat.BlockPlayerAsync(targetId);
                else         await _chat.UnblockPlayerAsync(targetId);
            }
            catch (System.Exception ex)
            {
                SULog.Warn($"ReportService: chat-level block update failed ({ex.Message})", SULog.Channel.Social);
            }
        }
    }
}
