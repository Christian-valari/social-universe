using NUnit.Framework;
using SocialUniverse.Economy;

namespace SocialUniverse.Tests
{
    public class LandBuildMathTests
    {
        [Test]
        public void EnsureSize_returns_array_of_requested_length_when_null()
        {
            var result = LandBuildMath.EnsureSize(null, 8);
            Assert.AreEqual(8, result.Length);
        }

        [Test]
        public void EnsureSize_preserves_existing_entries()
        {
            var slots = new[] { "a", null, "b" };
            var result = LandBuildMath.EnsureSize(slots, 8);
            Assert.AreEqual(8, result.Length);
            Assert.AreEqual("a", result[0]);
            Assert.AreEqual("b", result[2]);
        }

        [Test]
        public void EnsureSize_returns_same_instance_when_already_correct_length()
        {
            var slots = new string[8];
            Assert.AreSame(slots, LandBuildMath.EnsureSize(slots, 8));
        }

        [Test]
        public void FilledCount_counts_non_empty_entries()
        {
            Assert.AreEqual(2, LandBuildMath.FilledCount(new[] { "a", null, "", "b" }));
        }

        [Test]
        public void FilledCount_of_null_is_zero()
        {
            Assert.AreEqual(0, LandBuildMath.FilledCount(null));
        }

        [Test]
        public void IsEmpty_true_for_null_empty_or_out_of_range()
        {
            var slots = new[] { "a", null };
            Assert.IsFalse(LandBuildMath.IsEmpty(slots, 0));
            Assert.IsTrue(LandBuildMath.IsEmpty(slots, 1));
            Assert.IsTrue(LandBuildMath.IsEmpty(slots, 5));
            Assert.IsTrue(LandBuildMath.IsEmpty(null, 0));
        }
    }
}
