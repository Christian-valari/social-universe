using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Mining;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    // The closing-ring minigame in the real ActiveMining scene (parent scope: PlanetTestRootScope).
    // Rings are long, so quick test taps grade Perfect, unless a test says otherwise. The runner reads
    // the handoff when Start is pressed, so a test can begin its own run before pressing it.
    public class ActiveMiningSceneFlowTests
    {
        private const string ActiveMiningScenePath = "Assets/Scenes/ActiveMining.unity";

        private GameObject               _rootScopeObject;
        private ActiveMiningHandoff      _handoff;
        private ActiveMiningMinigameView _view;
        private AsteroidDefinition       _definition;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _rootScopeObject = new GameObject("PlanetTestRootScope");
            UnityEngine.Object.DontDestroyOnLoad(_rootScopeObject);
            var rootScope = _rootScopeObject.AddComponent<PlanetTestRootScope>();
            _handoff = rootScope.Container.Resolve(typeof(ActiveMiningHandoff)) as ActiveMiningHandoff;

            _definition = ScriptableObject.CreateInstance<AsteroidDefinition>();
            BeginRun(targets: 2, maxMisses: 3);

            bool sceneReady = false;
            Action<SceneReadyEvent> onReady = _ => sceneReady = true;
            EventBus.Subscribe(onReady);

            yield return SceneManager.LoadSceneAsync(ActiveMiningScenePath, LoadSceneMode.Single);

            float timeout = Time.realtimeSinceStartup + 10f;
            while (!sceneReady && Time.realtimeSinceStartup < timeout)
                yield return null;
            EventBus.Unsubscribe(onReady);
            Assert.IsTrue(sceneReady, "ActiveMining scene never published SceneReadyEvent");

            _view = UnityEngine.Object.FindFirstObjectByType<ActiveMiningMinigameView>();
            Assert.IsNotNull(_view, "ActiveMiningMinigameView is not in the scene");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_rootScopeObject != null) UnityEngine.Object.Destroy(_rootScopeObject);
            if (_definition != null) UnityEngine.Object.Destroy(_definition);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Hitting_every_target_clears_the_run_and_shows_the_full_quantity()
        {
            PressStart();
            yield return TapNextTarget();
            yield return TapNextTarget();

            Assert.IsTrue(_handoff.HasResult);
            Assert.IsTrue(_handoff.Succeeded);
            Assert.AreEqual(1f, _handoff.BankedFraction, 0.001f);
            Assert.AreEqual("Perfect run!", ResultText());
            Assert.AreEqual("+80 minerals", RewardText());
            Assert.IsTrue(Private<GameObject>(_view, "_resultBanner").activeSelf, "The result banner comes up");
            Assert.IsFalse(Private<Text>(_view, "_bankedText").gameObject.activeSelf, "The HUD goes away");
        }

        [UnityTest]
        public IEnumerator A_clear_short_of_all_perfect_hits_says_cleared()
        {
            BeginRun(targets: 1, maxMisses: 3, perfectBySeconds: -1f); // no hit can be this early, so it grades Good
            PressStart();
            yield return TapNextTarget();

            Assert.IsTrue(_handoff.Succeeded);
            Assert.AreEqual("Cleared!", ResultText());
            Assert.AreEqual("+64 minerals", RewardText());
        }

        [UnityTest]
        public IEnumerator A_wrong_tap_adds_a_miss_and_the_target_stays()
        {
            PressStart();
            yield return WaitForLiveTarget();
            var before = LiveTarget().Target;

            TapMissArea();

            Assert.AreEqual("Misses: 1/3", ErrorText());
            Assert.AreSame(before, LiveTarget().Target);
        }

        [UnityTest]
        public IEnumerator Reaching_the_miss_limit_shows_what_was_banked()
        {
            BeginRun(targets: 2, maxMisses: 1);
            PressStart();
            yield return TapNextTarget();
            yield return WaitForLiveTarget();

            TapMissArea();

            Assert.IsFalse(_handoff.Succeeded);
            Assert.AreEqual("Run over", ResultText());
            Assert.AreEqual("+40 minerals", RewardText());
            Assert.IsNull(LiveTarget(), "The session leaves the last ring live, but the view hides it");
        }

        [UnityTest]
        public IEnumerator The_counter_climbs_as_targets_are_hit()
        {
            PressStart();
            Assert.AreEqual("+0 minerals", BankedText());
            Assert.AreEqual("0/2", ProgressText());

            yield return TapNextTarget();

            Assert.AreEqual("+40 minerals", BankedText());
            Assert.AreEqual("1/2", ProgressText());
        }

        [UnityTest]
        public IEnumerator A_run_that_banks_nothing_shows_no_reward()
        {
            BeginRun(targets: 2, maxMisses: 1);
            PressStart();
            yield return WaitForLiveTarget();

            TapMissArea();

            Assert.IsTrue(_handoff.HasResult);
            Assert.AreEqual("Run over", ResultText());
            Assert.AreEqual("No reward", RewardText());
        }

        [UnityTest]
        public IEnumerator An_untapped_ring_closes_in_on_its_marker_and_counts_as_a_miss()
        {
            BeginRun(targets: 2, maxMisses: 3, ringSeconds: 1f);
            PressStart();
            yield return WaitForLiveTarget();
            var view   = LiveTarget();
            var target = view.Target;
            var ring   = Private<RectTransform>(view, "_ring");

            yield return WaitUntil(() => target.Progress > 0.5f, "the ring to close halfway");
            yield return null;
            Assert.Less(ring.localScale.x, 1.7f, "Halfway through, the ring is most of the way in");
            Assert.Greater(ring.localScale.x, 1f, "The ring only touches the marker when it closes");

            yield return WaitUntil(() => target.Progress >= 1f, "the ring to close");
            Assert.AreEqual("Misses: 1/3", ErrorText());
            Assert.AreEqual("1/2", ProgressText());
            Assert.AreNotSame(target, view.Target, "A closed ring's marker goes away");
        }

        // A real raycast, not onClick: the transparent target must take the tap ahead of the full-screen
        // miss area, over a hit area that reaches past the marker. This test's stand-in asteroid is too
        // big for the camera to keep its targets on screen, so the target is moved to the middle.
        [UnityTest]
        public IEnumerator A_tap_hits_the_target_within_its_hit_area_and_the_miss_area_outside_it()
        {
            PressStart();
            yield return WaitForLiveTarget();
            yield return null; // raycasts only see what the canvas has drawn

            var view   = LiveTarget();
            var center = new Vector3(Screen.width / 2f, Screen.height / 2f);
            view.SetScreenPosition(center);
            float markerRadius = Private<Image>(view, "_marker").rectTransform.rect.width / 2f * view.transform.lossyScale.x;

            Assert.AreSame(view.gameObject, TopRaycastAt(center));
            Assert.AreSame(view.gameObject, TopRaycastAt(center + Vector3.right * markerRadius * 1.5f));
            Assert.AreSame(Private<Button>(_view, "_missAreaButton").gameObject, TopRaycastAt(center + Vector3.right * markerRadius * 2f));
        }

        // One ring is live at a time, so a whole run needs one view and one anchor, and hit effects stop
        // growing at the pool's limit.
        [UnityTest]
        public IEnumerator A_long_run_reuses_its_views_anchors_and_hit_effects()
        {
            BeginRun(targets: 6, maxMisses: 3);
            PressStart();
            for (int i = 0; i < 6; i++)
                yield return TapNextTarget();

            Assert.AreEqual(2, UnityEngine.Object.FindObjectsByType<ActiveMiningTargetView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                "The template and one clone");
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<ActiveMiningTargetPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);

            var hitVfx = Private<List<GameObject>>(_view, "_hitVfx");
            Assert.AreEqual(4, hitVfx.Count);
            Assert.IsTrue(hitVfx[1].GetComponent<ParticleSystem>().isPlaying, "The sixth hit replays the second effect");
        }

        // Opening the scene on its own: no run was begun, so nothing set the handoff's settings.
        [UnityTest]
        public IEnumerator A_run_without_settings_plays_the_default_tier()
        {
            _handoff.Begin("earth", "Iron#1", _definition, 80, settings: null);
            PressStart();
            yield return WaitForLiveTarget();

            Assert.AreEqual("0/8", ProgressText());
            Assert.AreEqual("Misses: 0/3", ErrorText());
        }

        [UnityTest]
        public IEnumerator Target_clones_stay_below_the_hud()
        {
            PressStart();
            yield return WaitForLiveTarget();

            var clone = LiveTarget().transform;
            var hud   = Private<Text>(_view, "_progressText").transform;
            Assert.Less(clone.GetSiblingIndex(), hud.GetSiblingIndex());
        }

        private void BeginRun(int targets, int maxMisses, float ringSeconds = 5f, float perfectBySeconds = 4f)
        {
            var tier = new ActiveMiningTierSettings(targets, ringSeconds, perfectBySeconds, goodBySeconds: 4.5f, maxMisses);
            var settings = new ActiveMiningRunSettings(1, tier, previousTier: null,
                perfectShare: 1f, goodShare: 0.8f, lateShare: 0.6f, spawnGapSeconds: 0f, missGraceSeconds: 0f);
            _handoff.Begin("earth", "Iron#1", _definition, 80, settings);
        }

        private void PressStart()   => Private<Button>(_view, "_startButton").onClick.Invoke();
        private void TapMissArea()  => Private<Button>(_view, "_missAreaButton").onClick.Invoke();

        private IEnumerator TapNextTarget()
        {
            yield return WaitForLiveTarget();
            Private<Button>(LiveTarget(), "_button").onClick.Invoke();
        }

        private IEnumerator WaitForLiveTarget() => WaitUntil(() => LiveTarget() != null, "a target to spawn");

        private static ActiveMiningTargetView LiveTarget() =>
            UnityEngine.Object.FindObjectsByType<ActiveMiningTargetView>(FindObjectsSortMode.None)
                .FirstOrDefault(view => view.Target != null);

        private static GameObject TopRaycastAt(Vector3 screenPoint)
        {
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screenPoint }, results);
            Assert.IsNotEmpty(results, $"Nothing under {screenPoint}");
            return results[0].gameObject;
        }

        private string ResultText()   => Private<Text>(_view, "_resultText").text;
        private string RewardText()   => Private<TMP_Text>(_view, "_rewardText").text;
        private string BankedText()   => Private<Text>(_view, "_bankedText").text;
        private string ProgressText() => Private<Text>(_view, "_progressText").text;
        private string ErrorText()    => Private<Text>(_view, "_errorText").text;

        private static IEnumerator WaitUntil(Func<bool> condition, string what)
        {
            float timeout = Time.realtimeSinceStartup + 5f;
            while (!condition() && Time.realtimeSinceStartup < timeout)
                yield return null;
            Assert.IsTrue(condition(), $"Timed out waiting for {what}");
        }

        private static T Private<T>(object target, string name) where T : class
        {
            var value = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(target) as T;
            Assert.IsNotNull(value, $"{target.GetType().Name}.{name} is not set");
            return value;
        }
    }
}
