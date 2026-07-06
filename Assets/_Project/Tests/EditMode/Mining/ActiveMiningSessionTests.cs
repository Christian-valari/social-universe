using NUnit.Framework;
using SocialUniverse.Mining;

namespace SocialUniverse.Tests
{
    public class ActiveMiningSessionTests
    {
        [Test]
        public void Reaching_required_taps_succeeds()
        {
            var session = new ActiveMiningSession(tapsRequired: 3, maxErrors: 3, sessionDurationSeconds: 10f);

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
            var session = new ActiveMiningSession(tapsRequired: 10, maxErrors: 3, sessionDurationSeconds: 10f);

            session.RegisterMiss();
            session.RegisterMiss();
            Assert.AreEqual(ActiveMiningStage.InProgress, session.Stage);
            session.RegisterMiss();

            Assert.AreEqual(ActiveMiningStage.Failed, session.Stage);
            Assert.AreEqual(3, session.ErrorCount);
        }

        [Test]
        public void Running_out_of_time_fails_the_session_even_with_no_misses()
        {
            var session = new ActiveMiningSession(tapsRequired: 10, maxErrors: 3, sessionDurationSeconds: 1f);

            session.Tick(0.5f);
            Assert.AreEqual(ActiveMiningStage.InProgress, session.Stage);
            Assert.AreEqual(0, session.ErrorCount, "time running out is not counted as a miss");

            session.Tick(0.6f); // total 1.1s > 1s session duration

            Assert.AreEqual(ActiveMiningStage.Failed, session.Stage);
            Assert.AreEqual(0, session.ErrorCount);
        }

        [Test]
        public void Hits_do_not_extend_or_reset_the_overall_timer()
        {
            var session = new ActiveMiningSession(tapsRequired: 10, maxErrors: 3, sessionDurationSeconds: 1f);

            session.Tick(0.9f);
            session.RegisterHit();
            session.Tick(0.2f); // total elapsed 1.1s -> the overall clock keeps counting regardless of hits

            Assert.AreEqual(ActiveMiningStage.Failed, session.Stage);
        }

        [Test]
        public void Terminal_stages_ignore_further_hits_misses_and_ticks()
        {
            var session = new ActiveMiningSession(tapsRequired: 1, maxErrors: 3, sessionDurationSeconds: 10f);
            session.RegisterHit(); // -> Success

            session.RegisterMiss();
            session.Tick(1000f); // would fail on time if terminal stages didn't ignore Tick

            Assert.AreEqual(ActiveMiningStage.Success, session.Stage);
            Assert.AreEqual(0, session.ErrorCount);
        }

        [Test]
        public void OnStageChanged_fires_on_terminal_transition_only()
        {
            var session = new ActiveMiningSession(tapsRequired: 2, maxErrors: 3, sessionDurationSeconds: 10f);
            int fireCount = 0;
            session.OnStageChanged += _ => fireCount++;

            session.RegisterHit(); // 1/2, no transition
            Assert.AreEqual(0, fireCount);

            session.RegisterHit(); // 2/2 -> Success
            Assert.AreEqual(1, fireCount);
        }
    }
}
