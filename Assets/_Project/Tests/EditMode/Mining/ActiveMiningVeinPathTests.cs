using NUnit.Framework;
using SocialUniverse.Mining;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class ActiveMiningVeinPathTests
    {
        private static readonly Vector2 Start = Vector2.zero;
        private static readonly Vector2 End   = new Vector2(100f, 0f);

        [Test]
        public void Distance_is_measured_to_the_nearest_point_on_the_segment()
        {
            Assert.AreEqual(30f, ActiveMiningVeinPath.DistanceToSegment(new Vector2(50f, 30f), Start, End), 0.01f);
            Assert.AreEqual(50f, ActiveMiningVeinPath.DistanceToSegment(new Vector2(-50f, 0f), Start, End), 0.01f);
            Assert.AreEqual(50f, ActiveMiningVeinPath.DistanceToSegment(new Vector2(130f, 40f), Start, End), 0.01f);
        }

        [Test]
        public void Progress_runs_from_0_to_1_and_clamps()
        {
            Assert.AreEqual(0.5f, ActiveMiningVeinPath.ProgressAlong(new Vector2(50f, 10f), Start, End), 0.01f);
            Assert.AreEqual(1f, ActiveMiningVeinPath.ProgressAlong(new Vector2(200f, 0f), Start, End), 0.01f);
            Assert.AreEqual(0f, ActiveMiningVeinPath.ProgressAlong(new Vector2(-50f, 0f), Start, End), 0.01f);
        }

        [Test]
        public void Both_work_on_a_line_at_any_angle()
        {
            var end = new Vector2(30f, 40f); // 50 long

            Assert.AreEqual(10f, ActiveMiningVeinPath.DistanceToSegment(new Vector2(-8f, 6f), Start, end), 0.01f);
            Assert.AreEqual(0.5f, ActiveMiningVeinPath.ProgressAlong(new Vector2(15f, 20f), Start, end), 0.01f);
        }

        // Both ends can land on one point when a rock is too small to part them.
        [Test]
        public void A_line_with_no_length_is_a_point_and_already_finished()
        {
            Assert.AreEqual(5f, ActiveMiningVeinPath.DistanceToSegment(new Vector2(3f, 4f), Start, Start), 0.01f);
            Assert.AreEqual(1f, ActiveMiningVeinPath.ProgressAlong(new Vector2(3f, 4f), Start, Start), 0.01f);
        }
    }
}
