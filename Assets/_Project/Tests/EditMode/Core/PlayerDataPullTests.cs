using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Core;

namespace SocialUniverse.Tests
{
    // A scene opens only after the player's server data has loaded: a step that fails asks the player
    // to retry (instead of opening with defaults) and is retried until it loads.
    public class PlayerDataPullTests
    {
        private int _asked;
        private string _askedWith;

        private Task Ask(string message)
        {
            _asked++;
            _askedWith = message;
            return Task.CompletedTask;
        }

        // A step that throws its first `failures` times, then succeeds.
        private static (string, Func<Task>) Step(string name, Dictionary<string, int> runs, int failures = 0) =>
            (name, () =>
            {
                runs[name] = runs.TryGetValue(name, out var n) ? n + 1 : 1;
                if (runs[name] <= failures) throw new InvalidOperationException($"{name} offline");
                return Task.CompletedTask;
            });

        [SetUp]
        public void SetUp()
        {
            _asked     = 0;
            _askedWith = null;
        }

        [Test]
        public async Task Every_step_runs_once_and_nobody_is_asked_when_all_load()
        {
            var runs = new Dictionary<string, int>();

            await PlayerDataPull.RunAsync(new[] { Step("wallet", runs), Step("fleet", runs) }, required: true, Ask);

            Assert.AreEqual(1, runs["wallet"]);
            Assert.AreEqual(1, runs["fleet"]);
            Assert.AreEqual(0, _asked);
        }

        // The reads don't depend on each other, so a scene load costs one round trip, not one per read.
        [Test]
        public async Task Every_step_starts_before_any_has_finished()
        {
            var started = new List<string>();
            var gate    = new TaskCompletionSource<bool>();
            (string, Func<Task>) Waiting(string name) => (name, () => { started.Add(name); return gate.Task; });

            var pull = PlayerDataPull.RunAsync(new[] { Waiting("wallet"), Waiting("fleet"), Waiting("idle run") }, required: true, Ask);

            CollectionAssert.AreEqual(new[] { "wallet", "fleet", "idle run" }, started);
            Assert.IsFalse(pull.IsCompleted);
            gate.SetResult(true);
            await pull;
        }

        [Test]
        public async Task A_failed_step_asks_the_player_then_retries_only_that_step()
        {
            var runs = new Dictionary<string, int>();

            await PlayerDataPull.RunAsync(new[] { Step("wallet", runs), Step("fleet", runs, failures: 1) }, required: true, Ask);

            Assert.AreEqual(1, _asked);
            Assert.IsFalse(string.IsNullOrEmpty(_askedWith), "the player is told why the game isn't opening");
            Assert.AreEqual(1, runs["wallet"], "a step that loaded is not fetched again");
            Assert.AreEqual(2, runs["fleet"]);
        }

        [Test]
        public async Task It_keeps_asking_until_every_step_has_loaded()
        {
            var runs = new Dictionary<string, int>();

            await PlayerDataPull.RunAsync(new[] { Step("fleet", runs, failures: 3) }, required: true, Ask);

            Assert.AreEqual(3, _asked);
            Assert.AreEqual(4, runs["fleet"]);
        }

        // Dev mode and standalone scenes run against mocks that answer with nothing; there the old
        // behavior stays: a failed step is logged and the scene opens with that step's defaults.
        [Test]
        public async Task When_not_required_a_failed_step_is_skipped_without_asking()
        {
            var runs = new Dictionary<string, int>();

            await PlayerDataPull.RunAsync(new[] { Step("wallet", runs, failures: 1), Step("fleet", runs) }, required: false, Ask);

            Assert.AreEqual(0, _asked);
            Assert.AreEqual(1, runs["wallet"]);
            Assert.AreEqual(1, runs["fleet"]);
        }

        [Test]
        public async Task Progress_reaches_one_as_the_steps_finish()
        {
            var runs     = new Dictionary<string, int>();
            var progress = new List<float>();

            await PlayerDataPull.RunAsync(new[] { Step("wallet", runs), Step("fleet", runs, failures: 1) }, required: true, Ask,
                progress.Add);

            CollectionAssert.IsOrdered(progress);
            Assert.AreEqual(1f, progress[progress.Count - 1], 1e-6f);
            Assert.Less(progress[0], 1f);
        }
    }
}
