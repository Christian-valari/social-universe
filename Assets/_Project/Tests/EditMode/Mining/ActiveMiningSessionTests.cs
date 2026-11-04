using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Mining;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class ActiveMiningSessionTests
    {
        private const float Step = 0.05f;

        // Ring 1.6s, Perfect by 0.8s, Good by 1.2s; shares 1 / 0.8 / 0.6.
        private static ActiveMiningRunSettings Settings(int targets, int maxMisses, float spawnGap = 0.25f, float missGrace = 0.2f, float decoyChance = 0f) =>
            new ActiveMiningRunSettings(1,
                new ActiveMiningTierSettings(targets, 1.6f, 0.8f, 1.2f, maxMisses, decoyChance), null,
                1f, 0.8f, 0.6f, spawnGap, missGrace);

        private static ActiveMiningSession New(int targets = 8, int maxMisses = 3) =>
            new ActiveMiningSession(Settings(targets, maxMisses));

        private static void Advance(ActiveMiningSession s, float seconds)
        {
            int steps = Mathf.RoundToInt(seconds / Step);
            for (int i = 0; i < steps; i++) s.Tick(Step);
        }

        // Ticks until the next target has spawned.
        private static void WaitForTarget(ActiveMiningSession s)
        {
            for (int i = 0; i < 100 && s.LiveTargets.Count == 0 && s.Stage == ActiveMiningStage.InProgress; i++)
                s.Tick(Step);
            Assert.AreEqual(1, s.LiveTargets.Count, "the next target should have spawned");
        }

        private static void HitAt(ActiveMiningSession s, float seconds)
        {
            WaitForTarget(s);
            Advance(s, seconds);
            s.TapTarget(s.LiveTargets[0].Id);
        }

        private static void LetRingClose(ActiveMiningSession s)
        {
            WaitForTarget(s);
            Advance(s, 1.65f);
        }

        // A decoy rides along with a target when its roll comes in under the tier's chance. Rolls are
        // dealt in order and the last one repeats.
        private static ActiveMiningSession NewWithDecoys(float chance = 0.25f, int targets = 8, int maxMisses = 3,
            float spawnGap = 0.25f, float missGrace = 0.2f, float[] rolls = null)
        {
            var queue = new Queue<float>(rolls == null ? new[] { 0.1f } : rolls);
            float last = queue.Peek();
            return new ActiveMiningSession(Settings(targets, maxMisses, spawnGap, missGrace, chance),
                () => { if (queue.Count > 0) last = queue.Dequeue(); return last; });
        }

        private static ActiveMiningTarget Real(ActiveMiningSession s) =>
            s.LiveTargets.FirstOrDefault(t => t.Kind == ActiveMiningTargetKind.Normal);

        private static ActiveMiningTarget Decoy(ActiveMiningSession s) =>
            s.LiveTargets.FirstOrDefault(t => t.Kind == ActiveMiningTargetKind.Decoy);

        private static void WaitForReal(ActiveMiningSession s)
        {
            for (int i = 0; i < 100 && Real(s) == null && s.Stage == ActiveMiningStage.InProgress; i++)
                s.Tick(Step);
            Assert.IsNotNull(Real(s), "the next real target should have spawned");
        }

        private static void HitRealAt(ActiveMiningSession s, float seconds)
        {
            WaitForReal(s);
            Advance(s, seconds);
            s.TapTarget(Real(s).Id);
        }

        [Test]
        public void A_decoy_spawns_with_a_target_when_the_roll_is_under_the_chance()
        {
            var s = NewWithDecoys(0.25f, rolls: new[] { 0.1f });
            var spawned = new List<ActiveMiningTarget>();
            s.OnTargetSpawned += spawned.Add;

            s.Tick(0f);

            Assert.AreEqual(2, s.LiveTargets.Count);
            Assert.AreEqual(1, s.LiveTargets.Count(t => t.Kind == ActiveMiningTargetKind.Decoy));
            Assert.AreEqual(ActiveMiningTargetKind.Normal, s.LiveTargets[0].Kind, "The real target comes first");
            CollectionAssert.AreEqual(s.LiveTargets, spawned);
        }

        [Test]
        public void A_decoy_has_the_same_ring_as_a_target()
        {
            var s = NewWithDecoys();
            s.Tick(0f);

            Assert.AreEqual(Real(s).RingSeconds, Decoy(s).RingSeconds);
            Assert.AreEqual(0f, Decoy(s).Progress);
        }

        [TestCase(0.5f, 0.25f, TestName = "No_decoy_spawns_when_the_roll_is_over_the_chance")]
        [TestCase(0.25f, 0.25f, TestName = "No_decoy_spawns_when_the_roll_equals_the_chance")]
        [TestCase(0f, 0f, TestName = "No_decoy_spawns_in_a_tier_with_no_chance_whatever_the_roll")]
        public void No_decoy_spawns(float roll, float chance)
        {
            var s = NewWithDecoys(chance, rolls: new[] { roll });
            s.Tick(0f);

            Assert.AreEqual(1, s.LiveTargets.Count);
            Assert.IsNull(Decoy(s));
        }

        [Test]
        public void The_roll_is_made_once_per_real_target()
        {
            int rolls = 0;
            var s = new ActiveMiningSession(Settings(3, 3, decoyChance: 0.25f), () => { rolls++; return 0.9f; });

            for (int i = 0; i < 3; i++) HitRealAt(s, 0.3f);

            Assert.AreEqual(3, rolls);
        }

        [Test]
        public void Tapping_a_decoy_is_a_miss_that_costs_no_share()
        {
            var s = NewWithDecoys();
            var resolved = new List<(ActiveMiningTarget target, ActiveMiningGrade grade)>();
            s.OnTargetResolved += (target, grade) => resolved.Add((target, grade));
            s.Tick(0f);
            var decoy = Decoy(s);

            s.TapTarget(decoy.Id);

            Assert.AreEqual((1, 0, 0f), (s.ErrorCount, s.ResolvedTargets, s.BankedFraction));
            Assert.IsNotNull(Real(s), "The real target is still live");
            Assert.IsNull(Decoy(s));
            Assert.AreEqual(1, resolved.Count);
            Assert.AreSame(decoy, resolved[0].target);
            Assert.AreEqual(ActiveMiningGrade.Miss, resolved[0].grade);
        }

        [Test]
        public void Tapping_a_decoy_does_not_move_the_next_spawn()
        {
            var s = NewWithDecoys(rolls: new[] { 0.1f, 0.9f });
            s.Tick(0f);
            s.TapTarget(Decoy(s).Id);
            s.TapTarget(Real(s).Id);

            Advance(s, 0.2f);
            Assert.AreEqual(0, s.LiveTargets.Count);
            Advance(s, 0.1f);
            Assert.AreEqual(1, s.LiveTargets.Count, "The gap is the hit's, as if there were no decoy");
        }

        [Test]
        public void A_tap_just_after_a_decoy_is_forgiven_like_one_after_a_hit()
        {
            var s = NewWithDecoys();
            s.Tick(0f);
            s.TapTarget(Decoy(s).Id);

            s.TapOffTarget();

            Assert.AreEqual(1, s.ErrorCount);
        }

        [Test]
        public void Tapping_a_decoy_can_fail_the_run_with_a_target_still_live()
        {
            var s = NewWithDecoys(maxMisses: 1);
            s.Tick(0f);

            s.TapTarget(Decoy(s).Id);

            Assert.AreEqual(ActiveMiningStage.Failed, s.Stage);
        }

        [Test]
        public void An_ignored_decoy_disappears_at_no_cost()
        {
            var s = NewWithDecoys(rolls: new[] { 0.1f, 0.9f });
            var expired = new List<ActiveMiningTarget>();
            var resolved = new List<ActiveMiningTarget>();
            s.OnDecoyExpired += expired.Add;
            s.OnTargetResolved += (t, _) => resolved.Add(t);
            s.Tick(0f);
            var decoy = Decoy(s);

            s.TapTarget(Real(s).Id);
            Advance(s, 1.65f);

            Assert.AreEqual((0, 1), (s.ErrorCount, s.ResolvedTargets));
            CollectionAssert.AreEqual(new[] { decoy }, expired);
            CollectionAssert.DoesNotContain(resolved, decoy, "An ignored decoy is not a resolution");
            CollectionAssert.DoesNotContain(s.LiveTargets, decoy);
        }

        [Test]
        public void A_decoy_and_its_target_closing_together_cost_one_miss()
        {
            var s = NewWithDecoys(rolls: new[] { 0.1f, 0.9f });
            s.Tick(0f);

            Advance(s, 1.65f);

            Assert.AreEqual((1, 1), (s.ErrorCount, s.ResolvedTargets));
            Assert.IsNull(Decoy(s));
        }

        [Test]
        public void A_decoy_does_not_hold_back_the_next_target()
        {
            var s = NewWithDecoys(rolls: new[] { 0.1f, 0.9f });
            s.Tick(0f);
            var decoy = Decoy(s);

            s.TapTarget(Real(s).Id);
            Advance(s, 0.35f);

            Assert.IsNotNull(Real(s), "The next target spawned on the usual gap");
            Assert.Less(decoy.Progress, 1f);
            CollectionAssert.Contains(s.LiveTargets, decoy);
        }

        [Test]
        public void Decoys_do_not_count_toward_targets()
        {
            var s = NewWithDecoys(targets: 2, rolls: new[] { 0.1f });

            HitRealAt(s, 0.3f);
            Assert.AreEqual(ActiveMiningStage.InProgress, s.Stage);
            HitRealAt(s, 0.3f);

            Assert.AreEqual(ActiveMiningStage.Success, s.Stage);
            Assert.AreEqual((2, 0), (s.ResolvedTargets, s.ErrorCount));
            Assert.AreEqual(1f, s.BankedFraction, 0.0001f);
        }

        [Test]
        public void An_off_target_tap_with_only_a_decoy_live_is_ignored()
        {
            var s = NewWithDecoys(spawnGap: 0.5f, missGrace: 0f);
            int wrongTaps = 0;
            s.OnWrongTap += () => wrongTaps++;
            s.Tick(0f);
            s.TapTarget(Real(s).Id);
            Advance(s, 0.1f);
            Assert.IsNotNull(Decoy(s));
            Assert.IsNull(Real(s));

            s.TapOffTarget();

            Assert.AreEqual((0, 0), (s.ErrorCount, wrongTaps));
        }

        [Test]
        public void A_cleared_run_leaves_a_live_decoy_for_the_view_to_hide()
        {
            var s = NewWithDecoys(targets: 1);
            int expired = 0;
            s.OnDecoyExpired += _ => expired++;
            s.Tick(0f);

            s.TapTarget(Real(s).Id);
            s.Tick(5f);

            Assert.AreEqual(ActiveMiningStage.Success, s.Stage);
            Assert.IsNotNull(Decoy(s));
            Assert.AreEqual(0, expired);
        }

        [Test]
        public void The_first_tick_spawns_the_first_target()
        {
            var s = New();
            s.Tick(0f);
            Assert.AreEqual(1, s.LiveTargets.Count);
        }

        [Test]
        public void The_first_target_waits_for_the_lead_in()
        {
            var s = new ActiveMiningSession(new ActiveMiningRunSettings(1,
                new ActiveMiningTierSettings(8, 1.6f, 0.8f, 1.2f, 3), null,
                1f, 0.8f, 0.6f, 0.25f, 0.2f, firstTargetDelaySeconds: 0.6f));

            Advance(s, 0.5f);
            Assert.AreEqual(0, s.LiveTargets.Count, "Nothing is live during the lead-in");

            s.TapOffTarget();
            Assert.AreEqual(0, s.ErrorCount, "A stray tap during the lead-in is not a miss");

            Advance(s, 0.15f);
            Assert.AreEqual(1, s.LiveTargets.Count);
        }

        [Test]
        public void A_target_spawned_on_a_tick_starts_with_its_ring_open()
        {
            var s = New();
            var spawned = new List<ActiveMiningTarget>();
            s.OnTargetSpawned += spawned.Add;

            s.Tick(0.1f);

            Assert.AreEqual(1, spawned.Count);
            var t = s.LiveTargets[0];
            Assert.AreSame(spawned[0], t);
            Assert.AreEqual(ActiveMiningTargetKind.Normal, t.Kind);
            Assert.AreEqual(1.6f, t.RingSeconds, 0.0001f);
            Assert.AreEqual(0f, t.ElapsedSeconds);
            Assert.AreEqual(0f, t.Progress);
        }

        [Test]
        public void A_rings_progress_runs_from_open_to_closed()
        {
            var s = New();
            s.Tick(0f);
            var t = s.LiveTargets[0];

            Advance(s, 0.8f);

            Assert.AreEqual(0.5f, t.Progress, 0.001f);
        }

        // Off the exact boundaries, because Advance's 0.05s steps accumulate float error.
        [TestCase(0.75f, ActiveMiningGrade.Perfect)]
        [TestCase(0.85f, ActiveMiningGrade.Good)]
        [TestCase(1.15f, ActiveMiningGrade.Good)]
        [TestCase(1.25f, ActiveMiningGrade.Late)]
        [TestCase(1.55f, ActiveMiningGrade.Late)]
        public void Earlier_taps_grade_better(float tapAt, ActiveMiningGrade expected)
        {
            var s = New();
            var graded = new List<ActiveMiningGrade>();
            s.OnTargetResolved += (_, grade) => graded.Add(grade);

            HitAt(s, tapAt);

            CollectionAssert.AreEqual(new[] { expected }, graded);
            Assert.AreEqual(1, s.ResolvedTargets);
            Assert.AreEqual(0, s.ErrorCount);
        }

        [Test]
        public void A_closed_ring_is_a_miss_that_uses_up_the_target()
        {
            var s = New();
            var resolved = new List<(ActiveMiningTarget target, ActiveMiningGrade grade)>();
            s.OnTargetResolved += (target, grade) => resolved.Add((target, grade));
            s.Tick(0f);
            var t = s.LiveTargets[0];

            Advance(s, 1.65f);

            Assert.AreEqual((1, 1, 0f), (s.ErrorCount, s.ResolvedTargets, s.BankedFraction));
            Assert.AreEqual(0, s.LiveTargets.Count);
            Assert.AreEqual(1, resolved.Count);
            Assert.AreSame(t, resolved[0].target);
            Assert.AreEqual(ActiveMiningGrade.Miss, resolved[0].grade);
        }

        [Test]
        public void Banked_share_matches_the_spec_example()
        {
            // 8 targets: 6 Perfect, 1 Good, 1 Late
            var s = New(targets: 8);
            for (int i = 0; i < 6; i++) HitAt(s, 0.3f);
            HitAt(s, 1.0f);
            HitAt(s, 1.4f);

            Assert.AreEqual(ActiveMiningStage.Success, s.Stage);
            Assert.AreEqual((6, 1, 1), (s.PerfectCount, s.GoodCount, s.LateCount));
            Assert.AreEqual(0.925f, s.BankedFraction, 0.0001f);
        }

        [Test]
        public void The_miss_limit_fails_the_run_and_keeps_the_banked_share()
        {
            // 8 targets: 4 Perfect, then 3 closed rings
            var s = New(targets: 8, maxMisses: 3);
            for (int i = 0; i < 4; i++) HitAt(s, 0.3f);
            for (int i = 0; i < 3; i++) LetRingClose(s);

            Assert.AreEqual(ActiveMiningStage.Failed, s.Stage);
            Assert.AreEqual(3, s.ErrorCount);
            Assert.AreEqual(0.5f, s.BankedFraction, 0.0001f);
        }

        [Test]
        public void A_wrong_tap_adds_a_miss_but_the_target_and_its_ring_carry_on()
        {
            var s = New();
            int wrongTaps = 0;
            s.OnWrongTap += () => wrongTaps++;
            s.Tick(0f);
            var t = s.LiveTargets[0];
            Advance(s, 0.5f);

            s.TapOffTarget();

            Assert.AreEqual((1, 0, t), (s.ErrorCount, s.ResolvedTargets, s.LiveTargets[0]));
            Assert.GreaterOrEqual(t.ElapsedSeconds, 0.5f);
            Assert.AreEqual(1, wrongTaps);
        }

        [Test]
        public void A_wrong_tap_can_use_up_the_last_miss()
        {
            var s = New(maxMisses: 2);
            s.Tick(0f);

            s.TapOffTarget();
            Assert.AreEqual(ActiveMiningStage.InProgress, s.Stage);
            s.TapOffTarget();

            Assert.AreEqual(ActiveMiningStage.Failed, s.Stage);
            Assert.AreEqual(2, s.ErrorCount);
        }

        [Test]
        public void An_off_target_tap_just_after_a_hit_is_ignored()
        {
            var s = New();
            int wrongTaps = 0;
            s.OnWrongTap += () => wrongTaps++;
            HitAt(s, 0.3f);

            s.TapOffTarget();

            Assert.AreEqual(0, s.ErrorCount);
            Assert.AreEqual(0, wrongTaps);
        }

        [Test]
        public void An_off_target_tap_is_forgiven_only_until_the_grace_runs_out()
        {
            // No spawn gap, so the next target is live while the grace is still running.
            var s = new ActiveMiningSession(Settings(8, 3, spawnGap: 0f, missGrace: 0.2f));
            HitAt(s, 0.3f);
            s.Tick(0f);
            Assert.AreEqual(1, s.LiveTargets.Count);

            s.TapOffTarget();
            Assert.AreEqual(0, s.ErrorCount);

            Advance(s, 0.25f);
            s.TapOffTarget();
            Assert.AreEqual(1, s.ErrorCount);
        }

        [Test]
        public void An_off_target_tap_with_no_live_target_is_ignored()
        {
            var s = New();
            int wrongTaps = 0;
            s.OnWrongTap += () => wrongTaps++;

            s.TapOffTarget();

            Assert.AreEqual(0, s.ErrorCount);
            Assert.AreEqual(0, wrongTaps);
        }

        [Test]
        public void The_next_target_waits_for_the_spawn_gap()
        {
            var s = New();
            HitAt(s, 0.3f);

            Advance(s, 0.2f);
            Assert.AreEqual(0, s.LiveTargets.Count);

            Advance(s, 0.1f);
            Assert.AreEqual(1, s.LiveTargets.Count);
        }

        [Test]
        public void A_third_miss_on_the_last_target_is_a_failure_not_a_clear()
        {
            var s = New(targets: 3, maxMisses: 3);
            for (int i = 0; i < 3; i++) LetRingClose(s);

            Assert.AreEqual(ActiveMiningStage.Failed, s.Stage);
            Assert.AreEqual((3, 3, 0f), (s.ErrorCount, s.ResolvedTargets, s.BankedFraction));
        }

        [Test]
        public void Finished_runs_ignore_taps_and_ticks()
        {
            var s = New(targets: 2);
            HitAt(s, 0.3f);
            HitAt(s, 0.3f);
            Assert.AreEqual(ActiveMiningStage.Success, s.Stage);
            int events = 0;
            s.OnStageChanged += _ => events++;
            s.OnTargetSpawned += _ => events++;
            s.OnTargetResolved += (_, __) => events++;
            s.OnWrongTap += () => events++;
            var before = (s.ErrorCount, s.ResolvedTargets, s.BankedFraction, s.LiveTargets.Count);

            s.TapTarget(1);
            s.TapOffTarget();
            s.Tick(5f);

            Assert.AreEqual(before, (s.ErrorCount, s.ResolvedTargets, s.BankedFraction, s.LiveTargets.Count));
            Assert.AreEqual(ActiveMiningStage.Success, s.Stage);
            Assert.AreEqual(0, events);
        }

        [Test]
        public void A_failed_run_stops_aging_its_rings_and_ignores_taps()
        {
            var s = New(maxMisses: 1);
            s.Tick(0f);
            var t = s.LiveTargets[0];
            Advance(s, 0.5f);
            s.TapOffTarget();
            Assert.AreEqual(ActiveMiningStage.Failed, s.Stage);
            float elapsed = t.ElapsedSeconds;
            int events = 0;
            s.OnTargetResolved += (_, __) => events++;

            Advance(s, 2f);
            s.TapTarget(t.Id);

            Assert.AreEqual(elapsed, t.ElapsedSeconds);
            Assert.AreEqual((1, 0, 0f), (s.ErrorCount, s.ResolvedTargets, s.BankedFraction));
            Assert.AreEqual(0, events);
        }

        [Test]
        public void OnStageChanged_fires_once()
        {
            var s = New(targets: 3);
            var stages = new List<ActiveMiningStage>();
            s.OnStageChanged += stages.Add;

            HitAt(s, 0.3f);
            HitAt(s, 0.3f);
            CollectionAssert.IsEmpty(stages);
            HitAt(s, 0.3f);
            s.TapOffTarget();
            s.Tick(5f);

            CollectionAssert.AreEqual(new[] { ActiveMiningStage.Success }, stages);
        }

        [Test]
        public void Tapping_an_unknown_target_changes_nothing()
        {
            var s = New();
            int wrongTaps = 0;
            s.OnWrongTap += () => wrongTaps++;
            s.Tick(0f);

            s.TapTarget(999);

            Assert.AreEqual((0, 0, 1), (s.ErrorCount, s.ResolvedTargets, s.LiveTargets.Count));
            Assert.AreEqual(0, wrongTaps);
        }

        [Test]
        public void Tapping_the_id_of_a_resolved_target_does_not_hit_the_next_one()
        {
            var s = New();
            s.Tick(0f);
            int firstId = s.LiveTargets[0].Id;
            s.TapTarget(firstId);
            WaitForTarget(s);
            var next = s.LiveTargets[0];
            Assert.AreNotEqual(firstId, next.Id);

            s.TapTarget(firstId);

            Assert.AreEqual((1, 0, 1), (s.ResolvedTargets, s.ErrorCount, s.LiveTargets.Count));
            Assert.AreSame(next, s.LiveTargets[0]);
        }

        // A tap on a ring that has already closed is not a second miss.
        [Test]
        public void Tapping_a_target_whose_ring_already_closed_changes_nothing()
        {
            var s = New();
            s.Tick(0f);
            int id = s.LiveTargets[0].Id;
            Advance(s, 1.65f);

            s.TapTarget(id);

            Assert.AreEqual((1, 0f), (s.ErrorCount, s.BankedFraction));
        }

        // A hitch must not close a ring or burn through the miss limit in one frame.
        [Test]
        public void A_long_frame_costs_at_most_a_tenth_of_a_second()
        {
            var s = New();
            s.Tick(0f);
            var t = s.LiveTargets[0];

            s.Tick(10f);

            Assert.AreEqual(0.1f, t.ElapsedSeconds, 0.001f);
            Assert.AreEqual(0, s.ErrorCount);
        }
    }
}
