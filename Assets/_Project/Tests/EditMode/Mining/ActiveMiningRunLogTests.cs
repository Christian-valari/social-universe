using System.Globalization;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Mining;

namespace SocialUniverse.Tests
{
    public class ActiveMiningRunLogTests
    {
        private const float Step = 0.1f;

        // Ring 1.6s, Perfect by 0.8s, Good by 1.2s; shares 1 / 0.8 / 0.6, with no spawn gap or grace.
        private static ActiveMiningSession NewSession(int targets, int maxMisses) =>
            new ActiveMiningSession(new ActiveMiningRunSettings(1,
                new ActiveMiningTierSettings(targets, 1.6f, 0.8f, 1.2f, maxMisses),
                1f, 0.8f, 0.6f, 0f, 0f));

        private static void HitAfter(ActiveMiningSession session, int ticks)
        {
            session.Tick(0f);
            for (int i = 0; i < ticks; i++) session.Tick(Step);
            session.TapTarget(session.LiveTargets[0].Id);
        }

        [Test]
        public void A_run_logs_its_tier_grades_miss_causes_and_bank()
        {
            var session = NewSession(targets: 2, maxMisses: 3);
            HitAfter(session, 0); // fresh ring: Perfect
            HitAfter(session, 9); // 0.9s in: Good

            Assert.AreEqual("ActiveMining run: tier=3 perfect=1 good=1 late=0 rings=0 decoys=0 wrong=0 veins=0 banked=0.90 result=Success",
                ActiveMiningRunLog.Format(3, session));
        }

        [Test]
        public void A_run_that_ran_out_of_misses_logs_the_failure()
        {
            var session = NewSession(targets: 2, maxMisses: 1);
            session.Tick(0f);
            session.TapOffTarget();

            Assert.AreEqual("ActiveMining run: tier=2 perfect=0 good=0 late=0 rings=0 decoys=0 wrong=1 veins=0 banked=0.00 result=Failed",
                ActiveMiningRunLog.Format(2, session));
        }

        [Test]
        public void Each_miss_cause_is_counted_apart()
        {
            var session = new ActiveMiningSession(new ActiveMiningRunSettings(1,
                new ActiveMiningTierSettings(4, 1.6f, 0.8f, 1.2f, 5, 1f),
                1f, 0.8f, 0.6f, 0f, 0f), () => 0f);
            session.Tick(0f);
            session.TapTarget(session.LiveTargets[1].Id); // the decoy
            session.TapOffTarget();
            for (int i = 0; i < 17; i++) session.Tick(Step); // the ring closes

            Assert.AreEqual("ActiveMining run: tier=4 perfect=0 good=0 late=0 rings=1 decoys=1 wrong=1 veins=0 banked=0.00 result=InProgress",
                ActiveMiningRunLog.Format(4, session));
        }

        [Test]
        public void A_failed_vein_is_counted_apart_from_the_other_miss_causes()
        {
            var session = new ActiveMiningSession(new ActiveMiningRunSettings(5,
                new ActiveMiningTierSettings(4, 1.6f, 0.8f, 1.2f, 5, veinEvery: 1),
                1f, 0.8f, 0.6f, 0f, 0f));
            session.Tick(0f);
            session.FailVein(session.LiveTargets[0].Id);

            Assert.AreEqual("ActiveMining run: tier=5 perfect=0 good=0 late=0 rings=0 decoys=0 wrong=0 veins=1 banked=0.00 result=InProgress",
                ActiveMiningRunLog.Format(5, session));
        }

        [Test]
        public void A_late_hit_is_counted_apart_from_a_good_one()
        {
            var session = NewSession(targets: 2, maxMisses: 3);
            HitAfter(session, 13); // 1.3s in: Late

            StringAssert.Contains("good=0 late=1", ActiveMiningRunLog.Format(1, session));
        }

        [Test]
        public void The_bank_keeps_a_decimal_point_under_any_culture()
        {
            var previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            try
            {
                var session = NewSession(targets: 2, maxMisses: 3);
                HitAfter(session, 0);
                HitAfter(session, 9);

                StringAssert.Contains("banked=0.90", ActiveMiningRunLog.Format(1, session));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }
    }
}
