using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Safety;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class AudioManagerTests
    {
        private AudioCatalog     _catalog;
        private PlanetDefinition _planetWithBgm;
        private PlanetDefinition _planetWithoutBgm;
        private AudioClip        _planetClip;
        private AudioClip        _fallbackClip;

        private static void SetField(object target, string field, object value) =>
            target.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(target, value);

        [SetUp]
        public void SetUp()
        {
            _planetClip   = AudioClip.Create("planet", 1, 1, 44100, false);
            _fallbackClip = AudioClip.Create("fallback", 1, 1, 44100, false);

            _catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            SetField(_catalog, "_fallbackPlanetBgm", _fallbackClip);
            SetField(_catalog, "_sfxEntries", new[]
            {
                new SfxEntry { Id = SfxId.Confirm, Clip = _planetClip }
            });

            _planetWithBgm = ScriptableObject.CreateInstance<PlanetDefinition>();
            SetField(_planetWithBgm, "_bgmClip", _planetClip);

            _planetWithoutBgm = ScriptableObject.CreateInstance<PlanetDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_catalog);
            Object.DestroyImmediate(_planetWithBgm);
            Object.DestroyImmediate(_planetWithoutBgm);
            Object.DestroyImmediate(_planetClip);
            Object.DestroyImmediate(_fallbackClip);
        }

        [Test]
        public void ResolvePlanetBgm_returns_planets_own_clip_when_set()
        {
            Assert.AreEqual(_planetClip, AudioManager.ResolvePlanetBgm(_planetWithBgm, _catalog));
        }

        [Test]
        public void ResolvePlanetBgm_falls_back_to_catalog_when_planet_clip_null()
        {
            Assert.AreEqual(_fallbackClip, AudioManager.ResolvePlanetBgm(_planetWithoutBgm, _catalog));
        }

        [Test]
        public void ResolvePlanetBgm_falls_back_when_planet_itself_null()
        {
            Assert.AreEqual(_fallbackClip, AudioManager.ResolvePlanetBgm(null, _catalog));
        }

        [Test]
        public void GetSfxClip_returns_mapped_clip()
        {
            Assert.AreEqual(_planetClip, _catalog.GetSfxClip(SfxId.Confirm));
        }

        [Test]
        public void GetSfxClip_returns_null_for_unmapped_id()
        {
            Assert.IsNull(_catalog.GetSfxClip(SfxId.Cancel));
        }

        // AudioCatalog.asset stores each entry's id as a number, so a new id may only go on the end.
        [Test]
        public void New_sfx_ids_are_appended_so_catalog_numbers_hold()
        {
            Assert.AreEqual(13, (int)SfxId.BuildRemove);
            Assert.AreEqual(14, (int)SfxId.ActiveMiningMiss);
            Assert.AreEqual(15, (int)SfxId.ActiveMiningFail);
        }

        [Test]
        public void The_shipped_catalog_has_a_clip_for_the_active_mining_miss_and_fail_sounds()
        {
            var shipped = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioCatalog>("Assets/_Project/ScriptableObjects/AudioCatalog.asset");

            Assert.IsNotNull(shipped.GetSfxClip(SfxId.ActiveMiningMiss));
            Assert.IsNotNull(shipped.GetSfxClip(SfxId.ActiveMiningFail));
        }
    }
}
