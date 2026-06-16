using System.Reflection;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Social;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class ChatModerationFilterTests
    {
        private SocialConfig _config;

        [SetUp]
        public void SetUp() => _config = ScriptableObject.CreateInstance<SocialConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        private ChatModerationFilter MakeFilter(ChatFilterLevel level)
        {
            typeof(SocialConfig)
                .GetField("_chatFilterLevel", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_config, level);
            return new ChatModerationFilter(_config);
        }

        [Test]
        public void Clean_text_is_clean()
        {
            var filter = MakeFilter(ChatFilterLevel.Strict);

            Assert.IsTrue(filter.IsClean("hello fellow space miner!"));
        }

        [Test]
        public void Blocked_word_is_detected_case_insensitively()
        {
            var filter = MakeFilter(ChatFilterLevel.Strict);

            Assert.IsFalse(filter.IsClean("well ShIt happens"));
        }

        [Test]
        public void Letter_substitutions_are_detected()
        {
            var filter = MakeFilter(ChatFilterLevel.Strict);

            Assert.IsFalse(filter.IsClean("well sh1t happens"));
            Assert.IsFalse(filter.IsClean("$hit"));
        }

        [Test]
        public void Sanitize_masks_blocked_words_and_preserves_length()
        {
            var filter = MakeFilter(ChatFilterLevel.Moderate);
            var input  = "well shit happens";

            var masked = filter.Sanitize(input);

            Assert.AreEqual("well **** happens", masked);
            Assert.AreEqual(input.Length, masked.Length);
        }

        [Test]
        public void Strict_rejects_dirty_message()
        {
            var filter = MakeFilter(ChatFilterLevel.Strict);

            var verdict = filter.Apply("shit", out var output);

            Assert.AreEqual(ChatFilterVerdict.Rejected, verdict);
            Assert.IsNull(output);
        }

        [Test]
        public void Strict_allows_clean_message()
        {
            var filter = MakeFilter(ChatFilterLevel.Strict);

            var verdict = filter.Apply("hello!", out var output);

            Assert.AreEqual(ChatFilterVerdict.Allowed, verdict);
            Assert.AreEqual("hello!", output);
        }

        [Test]
        public void Moderate_masks_dirty_message()
        {
            var filter = MakeFilter(ChatFilterLevel.Moderate);

            var verdict = filter.Apply("well shit happens", out var output);

            Assert.AreEqual(ChatFilterVerdict.Masked, verdict);
            Assert.AreEqual("well **** happens", output);
        }

        [Test]
        public void Off_passes_everything_through()
        {
            var filter = MakeFilter(ChatFilterLevel.Off);

            var verdict = filter.Apply("shit", out var output);

            Assert.AreEqual(ChatFilterVerdict.Allowed, verdict);
            Assert.AreEqual("shit", output);
        }

        [Test]
        public void SanitizeIncoming_masks_at_strict_level()
        {
            var filter = MakeFilter(ChatFilterLevel.Strict);

            Assert.AreEqual("****", filter.SanitizeIncoming("shit"));
        }
    }
}
