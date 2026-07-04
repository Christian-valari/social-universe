using System.Reflection;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Mining;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class ActiveMiningMinigameTests
    {
        private EconomyConfig          _config;
        private MiningRewardCalculator _rewardCalc;
        private ActiveMiningMinigame   _minigame;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<EconomyConfig>();
            SetField(_config, "_activeMaxErrors", 3);
            SetField(_config, "_activeSecondsPerTap", 2f);
            SetField(_config, "_minActiveSessionSeconds", 0.1f);
            SetField(_config, "_maxActiveSessionSeconds", 999f);
            SetField(_config, "_activeYieldPerTap", 8f);
            SetField(_config, "_minActiveTaps", 1);
            SetField(_config, "_maxActiveTaps", 99);

            _rewardCalc = new MiningRewardCalculator(_config);
            _minigame   = new ActiveMiningMinigame(_config, _rewardCalc);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        private static void SetField(Object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        private Asteroid MakeAsteroid(int remainingYield = 8)
        {
            var def = ScriptableObject.CreateInstance<AsteroidDefinition>();
            var go  = new GameObject("TestAsteroid");
            var a   = go.AddComponent<Asteroid>();
            a.Initialize(def, "slot_0");
            typeof(Asteroid).GetField("<RemainingYield>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(a, remainingYield);
            return a;
        }

        [Test]
        public void Begin_creates_a_session_sized_from_the_reward_calculator()
        {
            bool started = _minigame.Begin(MakeAsteroid(remainingYield: 8)); // ceil(8/8)=1 tap, clamped up? min=1 so 1

            Assert.IsTrue(started);
            Assert.IsNotNull(_minigame.CurrentSession);
            Assert.AreEqual(1, _minigame.CurrentSession.TapsRequired);
            Assert.AreEqual(2f, _minigame.CurrentSession.SessionDurationSeconds, 0.001f);
        }

        [Test]
        public void Begin_fails_while_a_session_is_already_in_progress()
        {
            _minigame.Begin(MakeAsteroid());
            bool startedAgain = _minigame.Begin(MakeAsteroid());

            Assert.IsFalse(startedAgain);
        }

        [Test]
        public void RegisterTap_true_forwards_to_RegisterHit()
        {
            _minigame.Begin(MakeAsteroid(remainingYield: 8)); // 1 tap required
            _minigame.RegisterTap(true);

            Assert.AreEqual(ActiveMiningStage.Success, _minigame.CurrentSession.Stage);
        }

        [Test]
        public void Clear_releases_the_current_session()
        {
            _minigame.Begin(MakeAsteroid());
            _minigame.Clear();

            Assert.IsNull(_minigame.CurrentSession);
        }

        [Test]
        public void OnSessionChanged_fires_when_Begin_starts_a_session()
        {
            ActiveMiningSession seen = null;
            _minigame.OnSessionChanged += s => seen = s;

            _minigame.Begin(MakeAsteroid());

            Assert.IsNotNull(seen);
        }
    }
}
