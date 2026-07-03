using System;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Mining;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class IdleMiningSessionTests
    {
        private Asteroid MakeAsteroid()
        {
            var def = ScriptableObject.CreateInstance<Config.AsteroidDefinition>();
            var go  = new GameObject("TestAsteroid");
            var a   = go.AddComponent<Asteroid>();
            a.Initialize(def, "slot_0");
            return a;
        }

        [Test]
        public void New_session_starts_in_Traveling_when_duration_has_not_elapsed()
        {
            var session = new IdleMiningSession(MakeAsteroid(), DateTime.UtcNow, 60f);
            Assert.AreEqual(IdleMiningStage.Traveling, session.Stage);
        }

        [Test]
        public void Reconstructing_with_a_past_startUtc_that_exceeds_duration_starts_ReadyToClaim()
        {
            // Simulates restoring a persisted session after the app was closed long enough
            // for the duration to have fully elapsed while it was closed.
            var startUtc = DateTime.UtcNow.AddSeconds(-120);
            var session  = new IdleMiningSession(MakeAsteroid(), startUtc, 60f);

            Assert.AreEqual(IdleMiningStage.ReadyToClaim, session.Stage);
            Assert.AreEqual(1f, session.MiningProgress01, 0.001f);
        }

        [Test]
        public void BeginMining_only_transitions_from_Traveling()
        {
            var session = new IdleMiningSession(MakeAsteroid(), DateTime.UtcNow, 60f);
            session.BeginMining();
            Assert.AreEqual(IdleMiningStage.Mining, session.Stage);

            session.BeginMining(); // no-op, already past Traveling
            Assert.AreEqual(IdleMiningStage.Mining, session.Stage);
        }

        [Test]
        public async Task Tick_flips_to_ReadyToClaim_once_real_time_reaches_duration()
        {
            var session = new IdleMiningSession(MakeAsteroid(), DateTime.UtcNow, 0.05f);
            Assert.AreEqual(IdleMiningStage.Traveling, session.Stage);

            await Task.Delay(100);
            session.Tick(0f); // deltaTime is unused for the ready check — real elapsed time drives it

            Assert.AreEqual(IdleMiningStage.ReadyToClaim, session.Stage);
        }

        [Test]
        public void Claim_only_succeeds_from_ReadyToClaim()
        {
            var session = new IdleMiningSession(MakeAsteroid(), DateTime.UtcNow.AddSeconds(-120), 60f);
            Assert.AreEqual(IdleMiningStage.ReadyToClaim, session.Stage);

            session.Claim();

            Assert.AreEqual(IdleMiningStage.Complete, session.Stage);
        }

        [Test]
        public void Claim_is_a_no_op_when_not_ReadyToClaim()
        {
            var session = new IdleMiningSession(MakeAsteroid(), DateTime.UtcNow, 60f);
            session.Claim();
            Assert.AreEqual(IdleMiningStage.Traveling, session.Stage);
        }

        [Test]
        public void OnStageChanged_fires_when_stage_transitions()
        {
            var session = new IdleMiningSession(MakeAsteroid(), DateTime.UtcNow, 60f);
            IdleMiningStage? seen = null;
            session.OnStageChanged += s => seen = s;

            session.BeginMining();

            Assert.AreEqual(IdleMiningStage.Mining, seen);
        }
    }
}
