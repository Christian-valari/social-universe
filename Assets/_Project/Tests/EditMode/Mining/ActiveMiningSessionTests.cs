using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Mining;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class ActiveMiningSessionTests
    {
        private Asteroid MakeAsteroid()
        {
            var def = ScriptableObject.CreateInstance<AsteroidDefinition>();
            var go  = new GameObject("TestAsteroid");
            var a   = go.AddComponent<Asteroid>();
            a.Initialize(def, "slot_0");
            return a;
        }

        [Test]
        public void Reaching_required_taps_succeeds()
        {
            var session = new ActiveMiningSession(MakeAsteroid(), tapsRequired: 3, maxErrors: 3, tapWindowSeconds: 1f);

            session.RegisterHit();
            session.RegisterHit();
            Assert.AreEqual(ActiveMiningStage.InProgress, session.Stage);
            session.RegisterHit();

            Assert.AreEqual(ActiveMiningStage.Success, session.Stage);
            Assert.AreEqual(3, session.SuccessfulTaps);
        }

        [Test]
        public void Reaching_max_errors_fails()
        {
            var session = new ActiveMiningSession(MakeAsteroid(), tapsRequired: 10, maxErrors: 3, tapWindowSeconds: 1f);

            session.RegisterMiss();
            session.RegisterMiss();
            Assert.AreEqual(ActiveMiningStage.InProgress, session.Stage);
            session.RegisterMiss();

            Assert.AreEqual(ActiveMiningStage.Failed, session.Stage);
            Assert.AreEqual(3, session.ErrorCount);
        }

        [Test]
        public void Tick_past_the_tap_window_counts_as_a_miss()
        {
            var session = new ActiveMiningSession(MakeAsteroid(), tapsRequired: 10, maxErrors: 3, tapWindowSeconds: 1f);

            session.Tick(0.5f);
            Assert.AreEqual(0, session.ErrorCount);
            session.Tick(0.6f); // total 1.1s > 1s window

            Assert.AreEqual(1, session.ErrorCount);
        }

        [Test]
        public void Hit_resets_the_window_timer()
        {
            var session = new ActiveMiningSession(MakeAsteroid(), tapsRequired: 10, maxErrors: 3, tapWindowSeconds: 1f);

            session.Tick(0.9f);
            session.RegisterHit();
            session.Tick(0.9f); // would have missed at 1.8s total if the timer hadn't reset

            Assert.AreEqual(0, session.ErrorCount);
        }

        [Test]
        public void Terminal_stages_ignore_further_hits_misses_and_ticks()
        {
            var session = new ActiveMiningSession(MakeAsteroid(), tapsRequired: 1, maxErrors: 3, tapWindowSeconds: 1f);
            session.RegisterHit(); // -> Success

            session.RegisterMiss();
            session.Tick(10f);

            Assert.AreEqual(ActiveMiningStage.Success, session.Stage);
            Assert.AreEqual(0, session.ErrorCount);
        }

        [Test]
        public void OnStageChanged_fires_on_terminal_transition_only()
        {
            var session = new ActiveMiningSession(MakeAsteroid(), tapsRequired: 2, maxErrors: 3, tapWindowSeconds: 1f);
            int fireCount = 0;
            session.OnStageChanged += _ => fireCount++;

            session.RegisterHit(); // 1/2, no transition
            Assert.AreEqual(0, fireCount);

            session.RegisterHit(); // 2/2 -> Success
            Assert.AreEqual(1, fireCount);
        }
    }
}
