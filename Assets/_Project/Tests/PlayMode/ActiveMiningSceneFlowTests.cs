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
using SocialUniverse.Safety;
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
        private ActiveMiningRunStore     _runStore;
        private ActiveMiningMinigameView _view;
        private AsteroidDefinition       _definition;
        private RecordingAudio           _audio;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            PlayerPrefs.DeleteKey(SaveKeys.ActiveMiningRun);

            _rootScopeObject = new GameObject("PlanetTestRootScope");
            UnityEngine.Object.DontDestroyOnLoad(_rootScopeObject);
            var rootScope = _rootScopeObject.AddComponent<PlanetTestRootScope>();
            _handoff  = rootScope.Container.Resolve(typeof(ActiveMiningHandoff)) as ActiveMiningHandoff;
            _runStore = new ActiveMiningRunStore(rootScope.Container.Resolve(typeof(IAuthService)) as IAuthService);

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

            _audio = new RecordingAudio();
            SetPrivate(_view, "_audio", _audio);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = 0f;

            // A marker left behind would be settled by the next Planet-scene test. The scene outlives
            // this test, so clearing the handoff also stops its quit hook from saving another when
            // play mode ends.
            _handoff?.Clear();
            PlayerPrefs.DeleteKey(SaveKeys.ActiveMiningRun);

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
            CollectionAssert.DoesNotContain(_audio.Played, SfxId.ActiveMiningFail, "A clear is not a failure");
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
        public IEnumerator Nothing_is_saved_until_the_player_presses_start()
        {
            _view.SendMessage("OnApplicationPause", true);
            Assert.IsNull(_runStore.Load());

            PressStart();
            Assert.AreEqual("Iron#1", _runStore.Load()?.SlotId);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Pausing_the_app_saves_what_the_run_has_banked()
        {
            PressStart();
            yield return TapNextTarget();
            Assert.AreEqual(0f, _runStore.Load().BankedFraction, "A hit alone doesn't save");

            _view.SendMessage("OnApplicationPause", true);

            Assert.AreEqual(0.5f, _runStore.Load().BankedFraction, 0.001f);
        }

        [UnityTest]
        public IEnumerator Quitting_the_app_saves_what_the_run_has_banked()
        {
            PressStart();
            yield return TapNextTarget();

            _view.SendMessage("OnApplicationQuit");

            Assert.AreEqual(0.5f, _runStore.Load().BankedFraction, 0.001f);
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
            var model = GameObject.CreatePrimitive(PrimitiveType.Sphere); // a rock 0.12 in radius, collider and mesh alike
            model.transform.localScale = Vector3.one * 0.24f;
            SetPrivate(_definition, "_modelPrefab", model);
            stage.SpawnClone(_definition);
            UnityEngine.Object.Destroy(model);
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

        // The prefab's sphere collider encloses the rock; a marker on it can hang in empty space.
        [UnityTest]
        public IEnumerator A_target_is_placed_inside_the_rock_not_on_the_sphere_around_it()
        {
            var stage = Private<ActiveMiningAsteroidStage>(_view, "_stage");
            var model = GameObject.CreatePrimitive(PrimitiveType.Sphere); // a rock 0.05 in radius in a collider of 0.3
            model.transform.localScale = Vector3.one * 0.1f;
            UnityEngine.Object.DestroyImmediate(model.GetComponent<Collider>());
            model.AddComponent<SphereCollider>().radius = 3f;
            SetPrivate(_definition, "_modelPrefab", model);
            stage.SpawnClone(_definition);
            UnityEngine.Object.Destroy(model);
            Assert.AreEqual(0.3f, stage.ColliderRadius, 0.001f);

            PressStart();
            yield return WaitForLiveTarget();

            var placed = UnityEngine.Object.FindFirstObjectByType<ActiveMiningTargetPoint>().transform.position;
            Assert.AreEqual(0.05f, stage.SurfaceRadius, 0.001f);
            float distance = Vector3.Distance(stage.StageClone.transform.position, placed);
            Assert.AreEqual(stage.SurfaceRadius * ActiveMiningMinigameView.PlacementInset, distance, 0.001f);
            Assert.Less(distance, stage.SurfaceRadius, "Inside the rock's bounds, nowhere near the collider's sphere");
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

        [UnityTest]
        public IEnumerator A_perfect_hit_shows_its_grade_where_the_target_was()
        {
            PressStart();
            yield return WaitForLiveTarget();
            var     view = LiveTarget();
            Vector3 at   = view.transform.position;

            Private<Button>(view, "_button").onClick.Invoke();

            var label = Private<Text>(_view, "_gradeText");
            Assert.AreEqual("Perfect", GradeText());
            Assert.IsTrue(label.gameObject.activeInHierarchy, "The label is showing");
            Assert.AreEqual(1f, label.color.a, 0.001f);
            Assert.Less(Vector3.Distance(at, label.transform.position), 0.01f);
            CollectionAssert.AreEqual(new[] { SfxId.ActiveMiningTap }, _audio.Played, "A hit has no miss feedback");
        }

        [UnityTest]
        public IEnumerator A_hit_after_the_perfect_cut_off_shows_good()
        {
            BeginRun(targets: 1, maxMisses: 3, perfectBySeconds: -1f);
            PressStart();
            yield return TapNextTarget();

            Assert.AreEqual("Good", GradeText());
        }

        [UnityTest]
        public IEnumerator A_hit_after_the_good_cut_off_shows_late()
        {
            BeginRun(targets: 1, maxMisses: 3, perfectBySeconds: -2f, goodBySeconds: -1f);
            PressStart();
            yield return TapNextTarget();

            Assert.AreEqual("Late", GradeText());
        }

        [UnityTest]
        public IEnumerator A_wrong_tap_shows_a_red_miss_beside_the_live_target_not_over_it()
        {
            PressStart();
            yield return WaitForLiveTarget();
            var     ring      = LiveTarget();
            Vector2 at        = ring.transform.position;
            var     canvas    = _view.GetComponent<Canvas>();
            float   clearance = ((RectTransform)ring.transform).rect.height * canvas.scaleFactor;

            TapMissArea();

            var label = Private<Text>(_view, "_gradeText");
            Assert.AreEqual("Miss", GradeText());
            Assert.IsTrue(label.gameObject.activeInHierarchy, "The label is showing");
            Assert.Greater(label.color.r, 0.8f);
            Assert.Less(label.color.g, 0.5f);
            Assert.Less(label.color.b, 0.5f);
            Assert.AreEqual(clearance, Vector2.Distance(at, label.transform.position), 0.5f, "The label is a target's height from the ring");
            Assert.IsTrue(canvas.pixelRect.Contains(label.transform.position), "and still on screen");
        }

        [UnityTest]
        public IEnumerator A_wrong_tap_plays_the_miss_sound_and_shakes_the_asteroid_briefly()
        {
            Time.captureDeltaTime = 0.05f; // each frame is 0.05 s of game time
            PressStart();
            yield return WaitForLiveTarget();
            var clone = StageClone();
            var rest  = clone.transform.localPosition;

            TapMissArea();
            yield return null;

            CollectionAssert.AreEqual(new[] { SfxId.ActiveMiningMiss }, _audio.Played);
            Assert.Greater(Vector3.Distance(rest, clone.transform.localPosition), 0f, "The rock is shaking");

            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(0f, (clone.transform.localPosition - rest).sqrMagnitude, "A miss shakes for 0.2 s, then the rock sits where it was");
        }

        [UnityTest]
        public IEnumerator A_ring_that_closes_gets_the_same_miss_feedback()
        {
            Time.captureDeltaTime = 0.05f;
            BeginRun(targets: 2, maxMisses: 3, ringSeconds: 1f);
            PressStart();
            yield return WaitForLiveTarget();
            var target = LiveTarget().Target;
            var clone  = StageClone();
            var rest   = clone.transform.localPosition;

            yield return WaitUntil(() => target.Progress >= 1f, "the ring to close");

            Assert.AreEqual("Miss", GradeText());
            CollectionAssert.AreEqual(new[] { SfxId.ActiveMiningMiss }, _audio.Played);
            yield return null;
            Assert.Greater(Vector3.Distance(rest, clone.transform.localPosition), 0f, "The rock is shaking");
        }

        [UnityTest]
        public IEnumerator A_failed_run_plays_the_fail_sound_and_shakes_longer_than_a_miss()
        {
            Time.captureDeltaTime = 0.05f;
            BeginRun(targets: 2, maxMisses: 1);
            PressStart();
            yield return WaitForLiveTarget();
            var clone = StageClone();
            var rest  = clone.transform.localPosition;

            TapMissArea(); // the miss that ends the run

            CollectionAssert.AreEqual(new[] { SfxId.ActiveMiningFail }, _audio.Played, "The fail sound answers the final miss on its own");
            Assert.AreEqual("Miss", GradeText(), "though the label still says so");

            yield return new WaitForSeconds(0.3f);
            Assert.Greater(Vector3.Distance(rest, clone.transform.localPosition), 0f, "A miss alone would have stopped by now");

            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(0f, (clone.transform.localPosition - rest).sqrMagnitude, "The run's shake is over after 0.5 s");
        }

        [UnityTest]
        public IEnumerator A_ring_that_closes_on_the_final_miss_plays_only_the_fail_sound()
        {
            Time.captureDeltaTime = 0.05f;
            BeginRun(targets: 2, maxMisses: 1, ringSeconds: 1f);
            PressStart();
            yield return WaitUntil(() => Session().Stage == ActiveMiningStage.Failed, "the ring to close and end the run");

            CollectionAssert.AreEqual(new[] { SfxId.ActiveMiningFail }, _audio.Played);
        }

        [UnityTest]
        public IEnumerator A_grade_label_fades_out_over_half_a_second()
        {
            Time.captureDeltaTime = 0.05f;
            PressStart();
            yield return TapNextTarget();
            var label = Private<Text>(_view, "_gradeText");

            yield return new WaitForSeconds(0.25f);
            Assert.That(label.color.a, Is.InRange(0.2f, 0.8f), "Part way through its fade");

            yield return new WaitForSeconds(0.35f);
            Assert.IsFalse(label.gameObject.activeSelf, "Faded away");
        }

        [UnityTest]
        public IEnumerator The_pre_game_panel_shows_the_rules()
        {
            var rules = Private<Text>(_view, "_rulesText");

            StringAssert.StartsWith("Tap each target", rules.text);
            Assert.IsTrue(rules.gameObject.activeInHierarchy, "It shows with the pre-game panel");
            Assert.IsFalse(rules.raycastTarget, "It must not swallow the Start tap");
            yield return null;
        }

        // A lone word on a second line reads as a mistake. The rules are a single sentence for now.
        [UnityTest]
        public IEnumerator The_rules_sit_on_one_line()
        {
            var scaler = _view.GetComponent<CanvasScaler>(); // measured as on a phone 1080 px wide, where the canvas scales by 1
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            yield return null;
            Canvas.ForceUpdateCanvases();
            var rules = Private<Text>(_view, "_rulesText");

            Assert.Less(rules.preferredHeight, rules.fontSize * 1.5f, $"Wraps: {rules.preferredWidth} wide in a box {rules.rectTransform.rect.width} wide");
        }

        // Instructions that run off the screen, get cut off by their own box, or print over the
        // start prompt are as good as missing.
        [UnityTest]
        public IEnumerator The_rules_fit_on_screen_clear_of_the_start_prompt()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            var rules       = Private<Text>(_view, "_rulesText");
            var startPrompt = Private<Button>(_view, "_startButton").GetComponentInChildren<Text>();
            var mineralType = Private<Text>(_view, "_mineralTypeText");

            Assert.LessOrEqual(rules.preferredHeight, rules.rectTransform.rect.height, "The text isn't cut off by its box");

            Rect box    = WorldRect(rules.rectTransform);
            Rect screen = new Rect(0f, 0f, Screen.width, Screen.height);
            Assert.IsTrue(screen.Contains(box.min) && screen.Contains(box.max), $"{box} is within the screen {screen}");
            Assert.IsFalse(box.Overlaps(WorldRect(mineralType.rectTransform)), "It clears the mineral type text");
            Assert.IsFalse(box.Overlaps(RenderedTextRect(startPrompt)), "It clears the start prompt");
        }

        // Every sound in the minigame is silent without one, and Planet's is unloaded before this loads.
        [UnityTest]
        public IEnumerator The_scene_has_one_audio_listener()
        {
            var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)
                .Where(listener => listener.enabled && listener.gameObject.activeInHierarchy);

            Assert.AreEqual(1, listeners.Count());
            yield return null;
        }

        private void BeginRun(int targets, int maxMisses, float ringSeconds = 5f, float perfectBySeconds = 4f, float goodBySeconds = 4.5f)
        {
            var tier = new ActiveMiningTierSettings(targets, ringSeconds, perfectBySeconds, goodBySeconds, maxMisses);
            var settings = new ActiveMiningRunSettings(1, tier, previousTier: null,
                perfectShare: 1f, goodShare: 0.8f, lateShare: 0.6f, spawnGapSeconds: 0f, missGraceSeconds: 0f);
            _handoff.Begin("earth", "Iron#1", _definition, 80, settings);
        }

        private void PressStart()   => Private<Button>(_view, "_startButton").onClick.Invoke();
        private void TapMissArea()  => Private<Button>(_view, "_missAreaButton").onClick.Invoke();

        private GameObject StageClone() => Private<ActiveMiningAsteroidStage>(_view, "_stage").StageClone;

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
            return center + (Private<Camera>(_view, "_sceneCamera").transform.position - center).normalized * stage.SurfaceRadius * ActiveMiningMinigameView.PlacementInset;
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
        private string GradeText()    => Private<Text>(_view, "_gradeText").text;

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        // Where a middle-centre text's glyphs sit within its rect.
        private static Rect RenderedTextRect(Text text)
        {
            Rect  box  = WorldRect(text.rectTransform);
            var   size = new Vector2(text.preferredWidth, text.preferredHeight) * text.transform.lossyScale.x;
            return new Rect(box.center - size / 2f, size);
        }

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

        private sealed class RecordingAudio : IAudioManager
        {
            public readonly List<SfxId> Played = new List<SfxId>();

            public void PlaySfx(SfxId id) => Played.Add(id);
            public void PlayBgmForPlanet(PlanetDefinition planet) { }
            public void PlaySolarSystemBgm() { }
            public void PlayTravelBgm() { }
        }
    }
}
