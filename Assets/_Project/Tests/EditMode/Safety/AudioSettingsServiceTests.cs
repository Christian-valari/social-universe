using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Safety;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class AudioSettingsServiceTests
    {
        private AudioConfig _config;
        private AudioSettingsService _audio;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(SaveKeys.MusicVolume);
            PlayerPrefs.DeleteKey(SaveKeys.SfxVolume);

            _config = ScriptableObject.CreateInstance<AudioConfig>();
            _audio  = new AudioSettingsService(_config);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
            PlayerPrefs.DeleteKey(SaveKeys.MusicVolume);
            PlayerPrefs.DeleteKey(SaveKeys.SfxVolume);
        }

        [Test]
        public void Defaults_to_full_volume_when_nothing_persisted()
        {
            Assert.AreEqual(1f, _audio.MusicVolume01);
            Assert.AreEqual(1f, _audio.SfxVolume01);
        }

        [Test]
        public void SetMusicVolume_updates_property()
        {
            _audio.SetMusicVolume(0.4f);
            Assert.AreEqual(0.4f, _audio.MusicVolume01, 0.0001f);
        }

        [Test]
        public void SetSfxVolume_clamps_above_one()
        {
            _audio.SetSfxVolume(3f);
            Assert.AreEqual(1f, _audio.SfxVolume01);
        }

        [Test]
        public void SetMusicVolume_clamps_below_zero()
        {
            _audio.SetMusicVolume(-2f);
            Assert.AreEqual(0f, _audio.MusicVolume01);
        }

        [Test]
        public void SetMusicVolume_persists_across_fresh_instance()
        {
            _audio.SetMusicVolume(0.25f);

            var reloaded = new AudioSettingsService(_config);

            Assert.AreEqual(0.25f, reloaded.MusicVolume01, 0.0001f);
        }

        [Test]
        public void SetMusicVolume_fires_change_event_with_new_value()
        {
            float? raised = null;
            _audio.OnMusicVolumeChanged += v => raised = v;

            _audio.SetMusicVolume(0.6f);

            Assert.AreEqual(0.6f, raised.Value, 0.0001f);
        }

        [Test]
        public void SetSfxVolume_fires_change_event_with_new_value()
        {
            float? raised = null;
            _audio.OnSfxVolumeChanged += v => raised = v;

            _audio.SetSfxVolume(0.2f);

            Assert.AreEqual(0.2f, raised.Value, 0.0001f);
        }

        [TestCase(0f, -80f)]
        [TestCase(1f, 0f)]
        [TestCase(0.5f, -6.0206f)]
        public void LinearToDecibel_matches_standard_mixer_curve(float linear, float expectedDb)
        {
            Assert.AreEqual(expectedDb, AudioSettingsService.LinearToDecibel(linear), 0.01f);
        }
    }
}
