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
        private const float  ScreenEdgeTolerance   = 0.01f; // pixels of float error when a tap area exactly fills the margin

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
        // miss area, over a hit area that reaches past the marker.
        [UnityTest]
        public IEnumerator A_tap_hits_the_target_within_its_hit_area_and_the_miss_area_outside_it()
        {
            PressStart();
            yield return WaitForLiveTarget();
            yield return null; // raycasts only see what the canvas has drawn

            var view   = LiveTarget();
            var center = view.transform.position;
            var inward = Vector3.right * Mathf.Sign(Screen.width / 2f - center.x);
            float markerRadius = Private<Image>(view, "_marker").rectTransform.rect.width / 2f * view.transform.lossyScale.x;

            Assert.AreSame(view.gameObject, TopRaycastAt(center));
            Assert.AreSame(view.gameObject, TopRaycastAt(center + inward * markerRadius * 1.5f));
            Assert.AreSame(Private<Button>(_view, "_missAreaButton").gameObject, TopRaycastAt(center + inward * markerRadius * 2f));
        }

        // The stand-in primitive is far too big for this camera: nearly all of its surface is off screen.
        [UnityTest]
        public IEnumerator Every_target_on_an_asteroid_too_big_for_the_camera_has_its_tap_area_on_screen()
        {
            BeginRun(targets: 12, maxMisses: 3);
            PressStart();

            int spawned = 0;
            var offScreen = new List<string>();
            var screen = new Rect(-ScreenEdgeTolerance, -ScreenEdgeTolerance,
                Screen.width + 2f * ScreenEdgeTolerance, Screen.height + 2f * ScreenEdgeTolerance);
            Session().OnTargetSpawned += _ => // after the view's handler, so the target has just been placed
            {
                spawned++;
                var corners = new Vector3[4];
                ((RectTransform)LiveTarget().transform).GetWorldCorners(corners);
                if (!corners.All(corner => screen.Contains(corner)))
                    offScreen.Add($"Target {spawned}: {corners[0]} to {corners[2]}");
            };

            for (int i = 0; i < 12; i++)
                yield return TapNextTarget();

            Assert.AreEqual(12, spawned);
            CollectionAssert.IsEmpty(offScreen, $"Screen is {Screen.width}x{Screen.height}");
        }

        // Spawning on screen isn't enough: the spin mustn't carry the target off before its ring closes.
        // Some of this asteroid's surface is off screen, and it turns a sixth of the way round per ring.
        [UnityTest]
        public IEnumerator A_target_is_placed_where_it_stays_on_screen_until_its_ring_closes()
        {
            var stage = Private<ActiveMiningAsteroidStage>(_view, "_stage");
            var model = new GameObject("StandInModel");
            model.AddComponent<SphereCollider>().radius = 0.12f;
            SetPrivate(_definition, "_modelPrefab", model);
            stage.SpawnClone(_definition);
            SpinAsteroid(new Vector3(1f, 1f, 0f).normalized, degreesPerSecond: 60f);

            BeginRun(targets: 24, maxMisses: 3, ringSeconds: 1f);
            PressStart();

            int qualified = 0;
            var offScreen = new List<string>();
            float margin = 70f * _view.GetComponent<Canvas>().scaleFactor;
            Session().OnTargetSpawned += target =>
            {
                Vector3 placed = UnityEngine.Object.FindFirstObjectByType<ActiveMiningTargetPoint>().transform.position;
                if (Vector3.Distance(placed, PointFacingTheCamera()) < 0.0001f) return; // nothing qualified

                qualified++;
                if (!ProjectsOnScreen(placed, margin) || !ProjectsOnScreen(stage.PredictPoint(placed, target.RingSeconds), margin))
                    offScreen.Add($"Target {target.Id} placed at {placed}");
            };

            for (int i = 0; i < 24; i++)
                yield return TapNextTarget();

            Assert.Greater(qualified, 0, "This asteroid is meant to have places that qualify");
            CollectionAssert.IsEmpty(offScreen, $"Screen is {Screen.width}x{Screen.height}, margin {margin}");
        }

        // Too big for anything near the middle to stay on screen through 75 degrees of spin, so no
        // random placement can qualify.
        [UnityTest]
        public IEnumerator A_target_with_nowhere_that_stays_on_screen_directly_faces_the_camera()
        {
            SpinAsteroid(Vector3.up, degreesPerSecond: 15f);
            PressStart();

            Vector3 placed = Vector3.zero, onScreen = Vector3.zero;
            Session().OnTargetSpawned += _ =>
            {
                placed   = UnityEngine.Object.FindFirstObjectByType<ActiveMiningTargetPoint>().transform.position;
                onScreen = LiveTarget().transform.position;
            };

            yield return WaitForLiveTarget();

            Assert.Less(Vector3.Distance(PointFacingTheCamera(), placed), 0.0001f, $"Placed at {placed}");
            Assert.Less(Vector2.Distance(new Vector2(Screen.width / 2f, Screen.height / 2f), onScreen), 1f, $"On screen at {onScreen}");
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

        private ActiveMiningSession Session() => Private<ActiveMiningSessionRunner>(_view, "_runner").Session;

        private void SpinAsteroid(Vector3 axis, float degreesPerSecond)
        {
            var stage = Private<ActiveMiningAsteroidStage>(_view, "_stage");
            SetPrivate(stage, "_rotationAxis", axis);
            SetPrivate(stage, "_rotationSpeed", degreesPerSecond);
        }

        private Vector3 PointFacingTheCamera()
        {
            var stage  = Private<ActiveMiningAsteroidStage>(_view, "_stage");
            var center = stage.StageClone.transform.position;
            return center + (Private<Camera>(_view, "_sceneCamera").transform.position - center).normalized * stage.ColliderRadius;
        }

        private bool ProjectsOnScreen(Vector3 worldPoint, float margin)
        {
            var camera = Private<Camera>(_view, "_sceneCamera");
            return ActiveMiningTargetPoint.IsOnScreen(camera.WorldToScreenPoint(worldPoint),
                new Vector2(camera.pixelWidth, camera.pixelHeight), margin);
        }

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

        private static void SetPrivate(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"{target.GetType().Name} has no {name}");
            field.SetValue(target, value);
        }
    }
}
