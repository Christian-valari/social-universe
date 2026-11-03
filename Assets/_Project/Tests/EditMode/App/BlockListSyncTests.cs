using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.App;
using SocialUniverse.Core;
using SocialUniverse.Net;
using SocialUniverse.Social;

namespace SocialUniverse.Tests
{
    // The player's block list lives in Cloud Save ("blocked_users", written only by BlockUser). It is
    // pulled on sign-in and forgotten on sign-out, so chat filtering matches the server on every
    // device and the next account on this device starts clean.
    public class BlockListSyncTests
    {
        // ICloudSave that answers one default-class key, or throws for it.
        internal class ScriptedCloudSave : ICloudSave
        {
            public readonly Dictionary<string, object> Records = new();
            public Exception Throw;
            public readonly List<string> Loaded = new();

            public Task SaveAsync<T>(string key, T value) => Task.CompletedTask;

            public Task<T> LoadAsync<T>(string key, T defaultValue = default)
            {
                Loaded.Add(key);
                if (Throw != null) throw Throw;
                return Task.FromResult(Records.TryGetValue(key, out var v) && v is T typed ? typed : defaultValue);
            }

            public Task<T> LoadProtectedAsync<T>(string key, T defaultValue = default) => Task.FromResult(defaultValue);
            public Task DeleteAsync(string key) => Task.CompletedTask;
        }

        private ScriptedCloudSave _cloudSave;
        private ReportService     _reports;
        private BlockListSync     _sync;

        [SetUp]
        public void SetUp()
        {
            _cloudSave = new ScriptedCloudSave();
            _reports   = new ReportService(new FakeSocialBackendClient(), new FakeChatService());
            _sync      = new BlockListSync(_cloudSave, _reports);
        }

        [Test]
        public async Task LoadAsync_puts_the_servers_block_list_into_ReportService()
        {
            _cloudSave.Records[SaveKeys.BlockedUsers] = new List<string> { "griefer_42" };

            await _sync.LoadAsync();

            CollectionAssert.Contains(_cloudSave.Loaded, "blocked_users");
            Assert.IsTrue(_reports.HasBlockList);
            Assert.IsTrue(_reports.IsBlocked("griefer_42"));
        }

        [Test]
        public async Task LoadAsync_reads_a_missing_record_as_nobody_blocked()
        {
            _reports.SetBlockList(new[] { "stale" });

            await _sync.LoadAsync();

            Assert.IsTrue(_reports.HasBlockList);
            Assert.IsFalse(_reports.IsBlocked("stale"));
        }

        // A failed read must not look like "nobody blocked": the list stays unloaded so the Planet
        // scene's pull fetches it again.
        [Test]
        public void LoadAsync_lets_a_failed_read_through_and_leaves_the_list_unloaded()
        {
            _cloudSave.Throw = new InvalidOperationException("offline");

            Assert.ThrowsAsync<InvalidOperationException>(async () => await _sync.LoadAsync());
            Assert.IsFalse(_reports.HasBlockList);
        }
    }
}
