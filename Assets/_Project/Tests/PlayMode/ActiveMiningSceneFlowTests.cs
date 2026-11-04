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
            SpinAsteroid(new Vector3(1f, 1f, 0f).normalized);

            BeginRun(targets: 24, maxMisses: 3, ringSeconds: 1f, spinDegrees: 60f);
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
            SpinAsteroid(Vector3.up);
            BeginRun(targets: 2, maxMisses: 3, spinDegrees: 15f);
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

        // The settings in force when Start is pressed set the spin, since the dev tier picker can change them first.
        [UnityTest]
        public IEnumerator The_asteroid_spins_at_the_speed_the_run_settings_give()
        {
            BeginRun(targets: 2, maxMisses: 3, spinDegrees: 40f);
            PressStart();
            yield return WaitForLiveTarget();

            Assert.AreEqual(40f, Private<ActiveMiningAsteroidStage>(_view, "_stage").RotationSpeed, 0.001f);
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

        // No settings in the handoff to read the tier from, so the run's log line must not depend on them.
        [UnityTest]
        public IEnumerator A_run_without_settings_logs_as_the_default_tier()
        {
            _handoff.Begin("earth", "Iron#1", _definition, 80, settings: null);
            PressStart();
            yield return WaitForLiveTarget();
            TapMissArea();
            TapMissArea();

            LogAssert.Expect(LogType.Log, "[SU:Mining] ActiveMining run: tier=1 perfect=0 good=0 late=0 rings=0 decoys=0 wrong=3 veins=0 banked=0.00 result=Failed");
            TapMissArea();
        }

        // Nobody drags it, so its ring closes: that's a ring miss rather than a failed drag, and the
        // run still ends. It takes no pooled marker, which is also why a tap can't reach it.
        [UnityTest]
        public IEnumerator An_undragged_vein_closes_as_a_ring_miss_and_the_run_still_ends()
        {
            BeginRun(targets: 2, maxMisses: 3, ringSeconds: 1f, veinEvery: 1);
            PressStart();
            yield return WaitForLiveVein();
            var vein = VeinView().Target;

            Assert.IsNull(LiveView(ActiveMiningTargetKind.Vein), "A vein isn't shown as a marker");
            TapMissArea();
            Assert.AreEqual((0, 0), (Session().ErrorCount, Session().ResolvedTargets), "A tap neither completes it nor misses");

            yield return WaitUntil(() => vein.Progress >= 1f, "the vein's ring to close");
            Assert.AreEqual("Miss", GradeText());
            Assert.AreNotSame(vein, VeinView().Target, "A closed vein goes away");

            yield return WaitUntil(() => Session().Stage != ActiveMiningStage.InProgress, "the run to end");

            Assert.AreEqual(ActiveMiningStage.Success, Session().Stage);
            Assert.AreEqual((2, 2, 0), (Session().RingMisses, Session().ResolvedTargets, Session().VeinFails));
            Assert.IsFalse(VeinView().gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Dragging_start_to_end_completes_the_vein()
        {
            yield return BeginVein(targets: 2);
            var     vein  = VeinView();
            Vector2 start = vein.StartPosition, end = vein.EndPosition;

            vein.OnPointerDown(Pointer(start));
            vein.OnDrag(Pointer(Vector2.Lerp(start, end, 0.5f)));
            Assert.AreEqual((0, 0), (Session().ResolvedTargets, Session().ErrorCount), "Halfway isn't finished");

            vein.OnDrag(Pointer(Vector2.Lerp(start, end, 0.96f)));

            Assert.AreEqual((1, 0, 1), (Session().ResolvedTargets, Session().ErrorCount, Session().PerfectCount));
            Assert.AreEqual("1/2", ProgressText());
            Assert.AreEqual("Perfect", GradeText());
            Assert.IsNull(vein.Target);
            Assert.IsFalse(vein.gameObject.activeSelf, "The line goes away");
        }

        // Short of the threshold is still short: the end of the line has to be reached.
        [UnityTest]
        public IEnumerator A_drag_that_stops_short_of_the_threshold_does_not_complete()
        {
            yield return BeginVein(targets: 2);
            var     vein  = VeinView();
            Vector2 start = vein.StartPosition, end = vein.EndPosition;

            vein.OnPointerDown(Pointer(start));
            vein.OnDrag(Pointer(Vector2.Lerp(start, end, 0.93f)));

            Assert.AreEqual(0, Session().ResolvedTargets);
            Assert.IsTrue(Session().IsVeinLive);
        }

        // Canvas units on the config, screen pixels on the pointer: the stray limit scales with the canvas.
        [UnityTest]
        public IEnumerator The_stray_limit_is_in_canvas_units()
        {
            yield return BeginVein(targets: 3, maxMisses: 3, veinDragTolerance: 90f);
            var     vein  = VeinView();
            Vector2 start = vein.StartPosition, end = vein.EndPosition;
            Vector2 off   = Vector2.Perpendicular(end - start).normalized * 90f * _view.GetComponent<Canvas>().scaleFactor;
            Vector2 mid   = Vector2.Lerp(start, end, 0.5f);

            vein.OnPointerDown(Pointer(start));
            vein.OnDrag(Pointer(mid + off * 0.95f));
            Assert.AreEqual(0, Session().ErrorCount, "Just inside it");

            vein.OnDrag(Pointer(mid + off * 1.05f));
            Assert.AreEqual(1, Session().VeinFails, "Just outside it");
        }

        [UnityTest]
        public IEnumerator Lifting_early_fails_the_vein()
        {
            yield return BeginVein(targets: 2);
            var     vein  = VeinView();
            Vector2 start = vein.StartPosition, end = vein.EndPosition;

            vein.OnPointerDown(Pointer(start));
            vein.OnDrag(Pointer(Vector2.Lerp(start, end, 0.5f)));
            vein.OnPointerUp(Pointer(Vector2.Lerp(start, end, 0.5f)));

            Assert.AreEqual((1, 1, 1), (Session().ErrorCount, Session().ResolvedTargets, Session().VeinFails));
            Assert.AreEqual("Miss", GradeText());
            Assert.IsFalse(vein.gameObject.activeSelf);
        }

        // The UI module delivers a move and a release from the same frame as a pointer-up alone.
        [UnityTest]
        public IEnumerator A_lift_that_lands_on_the_end_of_the_line_completes_the_vein()
        {
            yield return BeginVein(targets: 2);
            var vein = VeinView();

            vein.OnPointerDown(Pointer(vein.StartPosition));
            vein.OnPointerUp(Pointer(vein.EndPosition));

            Assert.AreEqual((1, 0, 1), (Session().ResolvedTargets, Session().ErrorCount, Session().PerfectCount));
        }

        [UnityTest]
        public IEnumerator Straying_off_the_line_fails_the_vein()
        {
            yield return BeginVein(targets: 2);
            var     vein  = VeinView();
            Vector2 start = vein.StartPosition, end = vein.EndPosition;
            Vector2 away  = Vector2.Perpendicular(end - start).normalized * 500f;

            vein.OnPointerDown(Pointer(start));
            vein.OnDrag(Pointer(Vector2.Lerp(start, end, 0.5f) + away));

            Assert.AreEqual((1, 1, 1), (Session().ErrorCount, Session().ResolvedTargets, Session().VeinFails));
            Assert.AreEqual("Miss", GradeText());
        }

        // Straying first: a finger far off the line doesn't finish it just by getting past the end.
        [UnityTest]
        public IEnumerator Straying_past_the_end_of_the_line_fails_rather_than_completes()
        {
            yield return BeginVein(targets: 2);
            var     vein  = VeinView();
            Vector2 start = vein.StartPosition, end = vein.EndPosition;

            vein.OnPointerDown(Pointer(start));
            vein.OnDrag(Pointer(end + (end - start).normalized * 500f));

            Assert.AreEqual((1, 0), (Session().VeinFails, Session().PerfectCount));
        }

        // The rock turns under a finger that isn't moving, and no drag event says so.
        [UnityTest]
        public IEnumerator The_line_moving_off_a_still_finger_fails_the_vein()
        {
            yield return BeginVein(targets: 2);
            var     vein  = VeinView();
            Vector2 start = vein.StartPosition, end = vein.EndPosition;
            vein.OnPointerDown(Pointer(start));

            vein.UpdateLine(start + Vector2.up * 500f, end + Vector2.up * 500f);

            Assert.AreEqual(1, Session().VeinFails);
        }

        [UnityTest]
        public IEnumerator A_press_away_from_the_start_costs_nothing()
        {
            yield return BeginVein(targets: 2);
            var     vein  = VeinView();
            Vector2 start = vein.StartPosition, end = vein.EndPosition;
            Vector2 away  = start + Vector2.Perpendicular(end - start).normalized * 500f;

            vein.OnPointerDown(Pointer(away));
            vein.OnDrag(Pointer(end));
            vein.OnPointerUp(Pointer(end));

            Assert.AreEqual((0, 0), (Session().ErrorCount, Session().ResolvedTargets));
            Assert.IsTrue(Session().IsVeinLive);
            Assert.IsNotNull(vein.Target);
        }

        // The tap area is a circle the width of a normal target's hit area: all of it is within the
        // stray limit, where the corners of a square wouldn't be.
        [UnityTest]
        public IEnumerator A_press_counts_only_within_the_start_marker_tap_area()
        {
            yield return BeginVein(targets: 2);
            var     vein      = VeinView();
            Vector2 start     = vein.StartPosition, end = vein.EndPosition;
            float   tapRadius = 70f * _view.GetComponent<Canvas>().scaleFactor;
            Vector2 back      = (start - end).normalized;

            vein.OnPointerDown(Pointer(start + back * tapRadius * 1.05f));
            vein.OnPointerUp(Pointer(start + back * tapRadius * 1.05f));
            Assert.AreEqual(0, Session().ErrorCount, "Just outside, nothing was held to let go of");

            vein.OnPointerDown(Pointer(start + back * tapRadius * 0.95f));
            vein.OnPointerUp(Pointer(start + back * tapRadius * 0.95f));
            Assert.AreEqual(1, Session().VeinFails, "Just inside, it was held");
        }

        // A press on the rim of the tap area, on the far side from the end, is a finger still within
        // the stray limit, so it holds rather than failing on the next frame.
        [UnityTest]
        public IEnumerator A_press_at_the_rim_of_the_tap_area_is_not_a_stray()
        {
            yield return BeginVein(targets: 2);
            var     vein  = VeinView();
            Vector2 start = vein.StartPosition, end = vein.EndPosition;
            float   rim   = 69f * _view.GetComponent<Canvas>().scaleFactor;

            vein.OnPointerDown(Pointer(start + (start - end).normalized * rim));
            yield return null;
            yield return null;

            Assert.AreEqual(0, Session().ErrorCount);
            Assert.IsNotNull(vein.Target);
        }

        // A tighter stray limit brings the tap area in with it, or a press near its rim would fail at once.
        [UnityTest]
        public IEnumerator A_stray_limit_inside_the_tap_area_narrows_what_counts_as_a_press()
        {
            yield return BeginVein(targets: 2, veinDragTolerance: 40f);
            var     vein  = VeinView();
            Vector2 start = vein.StartPosition, end = vein.EndPosition;
            float   scale = _view.GetComponent<Canvas>().scaleFactor;

            vein.OnPointerDown(Pointer(start + (start - end).normalized * 60f * scale));
            yield return null;

            Assert.AreEqual(0, Session().ErrorCount, "Not held, so not a stray either");
            vein.OnPointerUp(Pointer(start));
            Assert.AreEqual(0, Session().ErrorCount, "and nothing to let go of");
        }

        [UnityTest]
        public IEnumerator A_second_finger_is_ignored()
        {
            yield return BeginVein(targets: 2);
            var     vein  = VeinView();
            Vector2 start = vein.StartPosition, end = vein.EndPosition;
            Vector2 away  = start + Vector2.Perpendicular(end - start).normalized * 500f;

            vein.OnPointerDown(Pointer(start, id: 7));

            vein.OnPointerDown(Pointer(start, id: 8));
            vein.OnDrag(Pointer(away, id: 8));
            vein.OnPointerUp(Pointer(away, id: 8));
            Assert.AreEqual((0, 0), (Session().ErrorCount, Session().ResolvedTargets), "The other finger strays and lifts at no cost");

            vein.OnDrag(Pointer(end, id: 8));
            Assert.AreEqual(0, Session().ResolvedTargets, "and can't finish it either");

            vein.OnDrag(Pointer(end, id: 7));
            Assert.AreEqual((1, 1), (Session().ResolvedTargets, Session().PerfectCount), "The first finger still holds it");
        }

        // Unity sends no pointer-up on pause, and a phone call shouldn't cost a miss.
        [UnityTest]
        public IEnumerator Pausing_the_app_lets_go_of_a_vein_at_no_cost_and_its_start_can_be_pressed_again()
        {
            yield return BeginVein(targets: 2);
            var     vein  = VeinView();
            Vector2 start = vein.StartPosition, end = vein.EndPosition;
            vein.OnPointerDown(Pointer(start));
            vein.OnDrag(Pointer(Vector2.Lerp(start, end, 0.5f)));

            vein.SendMessage("OnApplicationPause", true);

            vein.OnDrag(Pointer(end));
            vein.OnPointerUp(Pointer(end));
            Assert.AreEqual((0, 0), (Session().ErrorCount, Session().ResolvedTargets), "The hold is gone, so neither finishes nor fails it");
            Assert.IsTrue(Session().IsVeinLive);

            vein.SendMessage("OnApplicationPause", false);
            vein.OnPointerDown(Pointer(vein.StartPosition));
            vein.OnDrag(Pointer(vein.EndPosition));

            Assert.AreEqual((1, 0), (Session().ResolvedTargets, Session().ErrorCount));
        }

        // Losing focus, as when a system dialog takes the touch, ends the hold the same way a pause does.
        [UnityTest]
        public IEnumerator Losing_focus_lets_go_of_a_vein_at_no_cost_and_its_start_can_be_pressed_again()
        {
            yield return BeginVein(targets: 2);
            var     vein  = VeinView();
            Vector2 start = vein.StartPosition, end = vein.EndPosition;
            vein.OnPointerDown(Pointer(start));
            vein.OnDrag(Pointer(Vector2.Lerp(start, end, 0.5f)));

            vein.SendMessage("OnApplicationFocus", false);

            vein.OnDrag(Pointer(end));
            vein.OnPointerUp(Pointer(end));
            Assert.AreEqual((0, 0), (Session().ErrorCount, Session().ResolvedTargets), "The hold is gone, so neither finishes nor fails it");
            Assert.IsTrue(Session().IsVeinLive);

            vein.SendMessage("OnApplicationFocus", true);
            Assert.IsTrue(Session().IsVeinLive, "Getting focus back changes nothing");
            vein.OnPointerDown(Pointer(vein.StartPosition));
            vein.OnDrag(Pointer(vein.EndPosition));

            Assert.AreEqual((1, 0), (Session().ResolvedTargets, Session().ErrorCount));
        }

        // The ring closing is the miss. The finger that was still down is lifted later, and costs nothing more.
        [UnityTest]
        public IEnumerator A_ring_closing_during_a_hold_then_a_lift_costs_one_miss()
        {
            BeginRun(targets: 3, maxMisses: 5, ringSeconds: 1f, veinEvery: 1);
            PressStart();
            yield return WaitForLiveVein();
            var vein = VeinView();
            var held = vein.Target;
            Vector2 start = vein.StartPosition;

            vein.OnPointerDown(Pointer(start));
            while (held.Progress < 1f && vein.Target == held) // the rock turns, so the finger follows the start
            {
                vein.OnDrag(Pointer(vein.StartPosition));
                yield return null;
            }
            Assert.AreEqual((1, 1), (Session().ErrorCount, Session().ResolvedTargets), "The ring closing is one miss");

            vein.OnPointerUp(Pointer(start));

            Assert.AreEqual((1, 1), (Session().ErrorCount, Session().ResolvedTargets), "and the lift is not another");
        }

        // The finger that was down when a vein closed has no hold on the one that follows it.
        [UnityTest]
        public IEnumerator The_next_vein_does_not_inherit_a_held_finger()
        {
            BeginRun(targets: 3, maxMisses: 5, ringSeconds: 1f, veinEvery: 1);
            PressStart();
            yield return WaitForLiveVein();
            var vein  = VeinView();
            var first = vein.Target;
            vein.OnPointerDown(Pointer(vein.StartPosition));

            while (vein.Target == first)
            {
                vein.OnDrag(Pointer(vein.StartPosition));
                yield return null;
            }
            yield return WaitUntil(() => VeinView().Target != null, "the next vein to spawn");
            var next = vein.Target;
            Assert.AreNotSame(first, next);

            vein.OnDrag(Pointer(vein.EndPosition));
            vein.OnPointerUp(Pointer(vein.EndPosition));

            Assert.AreSame(next, vein.Target, "The old finger neither completes nor fails the new vein");
            Assert.IsTrue(Session().IsVeinLive);
            Assert.AreEqual((1, 1), (Session().ErrorCount, Session().ResolvedTargets), "Only the first vein's ring miss");
        }

        // The view covers the screen, so a press goes to it and not to the miss area beneath. A real raycast.
        [UnityTest]
        public IEnumerator A_live_vein_takes_a_press_anywhere_on_screen()
        {
            Assert.IsFalse(VeinView().gameObject.activeSelf, "Nothing to press before a vein");

            yield return BeginVein(targets: 2);
            yield return null; // raycasts only see what the canvas has drawn

            Assert.AreSame(VeinView().gameObject, TopRaycastAt(new Vector2(Screen.width / 2f, Screen.height / 2f)));
            Assert.AreSame(VeinView().gameObject, TopRaycastAt(new Vector2(5f, 5f)));
            Assert.AreSame(VeinView().gameObject, TopRaycastAt(VeinView().StartPosition));
        }

        [UnityTest]
        public IEnumerator A_vein_draws_its_line_between_the_projected_ends_each_frame()
        {
            SpawnRock(0.1f);
            BeginRun(targets: 2, maxMisses: 3, veinEvery: 1, spinDegrees: 30f, veinSpinDegrees: 30f);
            PressStart();
            yield return WaitForLiveVein();
            yield return null;

            var     vein   = VeinView();
            var     camera = Private<Camera>(_view, "_sceneCamera");
            float   scale  = _view.GetComponent<Canvas>().scaleFactor;
            Vector2 first  = vein.StartPosition;

            yield return new WaitForSeconds(0.1f);

            Vector2 start = vein.StartPosition, end = vein.EndPosition;
            Assert.Greater(Vector2.Distance(first, start), 0f, "The ends ride the spinning rock");
            Assert.Less(Vector2.Distance(start, (Vector2)camera.WorldToScreenPoint(Private<ActiveMiningTargetPoint>(_view, "_veinStart").transform.position)), 2f);
            Assert.Less(Vector2.Distance(end, (Vector2)camera.WorldToScreenPoint(Private<ActiveMiningTargetPoint>(_view, "_veinEnd").transform.position)), 2f);

            var line = Private<RectTransform>(vein, "_line");
            Assert.Less(Vector2.Distance((start + end) / 2f, line.position), 1f, "The line's middle is between the ends");
            Assert.AreEqual(Vector2.Distance(start, end), line.rect.width * scale, 1f, "and it reaches them");
            Assert.AreEqual(Vector2.SignedAngle(Vector2.right, end - start), Mathf.DeltaAngle(0f, line.eulerAngles.z), 0.5f);

            Assert.Less(Vector2.Distance(start, Private<RectTransform>(vein, "_startMarker").position), 0.01f);
            Assert.Less(Vector2.Distance(end, Private<RectTransform>(vein, "_endMarker").position), 0.01f);
            Assert.Less(Vector2.Distance(start, Private<RectTransform>(vein, "_ring").position), 0.01f, "The ring closes on the start");
        }

        [UnityTest]
        public IEnumerator A_vein_ring_closes_in_on_its_start_like_a_target_ring()
        {
            BeginRun(targets: 2, maxMisses: 3, ringSeconds: 1f, veinEvery: 1);
            PressStart();
            yield return WaitForLiveVein();
            var vein = VeinView().Target;
            var ring = Private<RectTransform>(VeinView(), "_ring");
            yield return null;
            Assert.Greater(ring.localScale.x, 1.8f, "It starts wide");

            yield return WaitUntil(() => vein.Progress > 0.5f, "the ring to close halfway");
            yield return null;
            Assert.Less(ring.localScale.x, 1.7f);
            Assert.Greater(ring.localScale.x, 1f);
        }

        // Both ends count, now and when the ring closes, on a rock with plenty of room.
        [UnityTest]
        public IEnumerator A_vein_ends_are_on_screen_until_its_ring_closes_and_within_the_length_band()
        {
            SpawnRock(0.2f);
            BeginRun(targets: 16, maxMisses: 99, ringSeconds: 1.6f, veinEvery: 1, spinDegrees: 40f);
            PressStart();

            var   stage  = Private<ActiveMiningAsteroidStage>(_view, "_stage");
            var   camera = Private<Camera>(_view, "_sceneCamera");
            float scale  = _view.GetComponent<Canvas>().scaleFactor;
            float margin = 70f * scale;

            int placed = 0;
            var wrong  = new List<string>();
            Session().OnTargetSpawned += vein => // after the view's handler, so the ends have just been placed
            {
                placed++;
                Vector3 start = Private<ActiveMiningTargetPoint>(_view, "_veinStart").transform.position;
                Vector3 end   = Private<ActiveMiningTargetPoint>(_view, "_veinEnd").transform.position;

                if (!ProjectsOnScreen(start, margin) || !ProjectsOnScreen(stage.PredictPoint(start, vein.RingSeconds), margin)
                    || !ProjectsOnScreen(end, margin) || !ProjectsOnScreen(stage.PredictPoint(end, vein.RingSeconds), margin))
                    wrong.Add($"Vein {vein.Id} off screen: {start} {end}");

                float length = Vector2.Distance(camera.WorldToScreenPoint(start), camera.WorldToScreenPoint(end));
                if (length < 300f * scale || length > 550f * scale) wrong.Add($"Vein {vein.Id} is {length} px long at scale {scale}, outside 300-550");
            };

            for (int i = 0; i < 16; i++)
            {
                yield return WaitForLiveVein();
                VeinView().OnPointerDown(Pointer(VeinView().StartPosition));
                VeinView().OnPointerUp(Pointer(VeinView().StartPosition));
                yield return null;
            }

            Assert.AreEqual(16, placed);
            CollectionAssert.IsEmpty(wrong, $"Screen is {Screen.width}x{Screen.height}");
        }

        // No pair on this rock is 300 apart, so the view takes the one nearest the band, which is the longest: on screen, and not one point.
        [UnityTest]
        public IEnumerator A_vein_on_a_rock_too_small_for_the_band_takes_the_longest_pair()
        {
            SpawnRock(0.02f);
            BeginRun(targets: 2, maxMisses: 3, veinEvery: 1);
            PressStart();
            yield return WaitForLiveVein();

            float   scale = _view.GetComponent<Canvas>().scaleFactor;
            float   rim   = 0.01f * ActiveMiningMinigameView.PlacementInset;
            Vector3 start = Private<ActiveMiningTargetPoint>(_view, "_veinStart").transform.position;
            Vector3 end   = Private<ActiveMiningTargetPoint>(_view, "_veinEnd").transform.position;
            var     center = StageClone().transform.position;
            float   length = Vector2.Distance(VeinView().StartPosition, VeinView().EndPosition);

            Assert.Less(length, 300f * scale);
            Assert.Greater(length, 0f, "The ends are apart");
            Assert.AreEqual(rim, Vector3.Distance(center, start), 0.001f, "and on the rock's surface, not the fallback");
            Assert.AreEqual(rim, Vector3.Distance(center, end), 0.001f);
        }

        // Too big for anything near the middle to stay on screen through 75 degrees of spin, so no random
        // pair can qualify: a level line through the middle, the full gap long, is still playable.
        [UnityTest]
        public IEnumerator A_vein_with_nowhere_that_stays_on_screen_lies_level_across_the_point_facing_the_camera()
        {
            SpinAsteroid(Vector3.up);
            BeginRun(targets: 2, maxMisses: 3, veinEvery: 1, spinDegrees: 15f, veinSpinDegrees: 15f);
            PressStart();
            yield return WaitForLiveVein();

            var     vein   = VeinView();
            float   scale  = _view.GetComponent<Canvas>().scaleFactor;
            Vector2 middle = new Vector2(Screen.width / 2f, Screen.height / 2f);

            Assert.AreEqual(300f * scale, Vector2.Distance(vein.StartPosition, vein.EndPosition), 2f);
            Assert.AreEqual(vein.StartPosition.y, vein.EndPosition.y, 2f, "Level");
            Assert.Less(Vector2.Distance(middle, (vein.StartPosition + vein.EndPosition) / 2f), 2f, "Across the middle");

            vein.OnPointerDown(Pointer(vein.StartPosition));
            vein.OnDrag(Pointer(vein.EndPosition));
            Assert.AreEqual(1, Session().ResolvedTargets, "and it can be played");
        }

        // A press that lands is shown on the start marker: bigger and gold, until it is let go.
        [UnityTest]
        public IEnumerator The_start_marker_shows_a_held_press()
        {
            yield return BeginVein(targets: 3);
            var     vein   = VeinView();
            var     marker = Private<RectTransform>(vein, "_startMarker");
            var     graphic = marker.GetComponent<UnityEngine.UI.Graphic>();
            Vector2 start  = vein.StartPosition, end = vein.EndPosition;
            var     target = vein.Target;
            Vector3 restScale = marker.localScale;
            Color   restColor = graphic.color;

            vein.OnPointerDown(Pointer(start + Vector2.Perpendicular(end - start).normalized * 400f));
            Assert.AreEqual(restScale, marker.localScale, "A press off the marker changes nothing");
            Assert.AreEqual(restColor, graphic.color);

            vein.OnPointerDown(Pointer(start));
            Assert.AreEqual(restScale.x * 1.25f, marker.localScale.x, 0.001f);
            Assert.AreEqual(new Color(1f, 0.84f, 0.3f), graphic.color);

            vein.OnDrag(Pointer(Vector2.Lerp(start, end, 0.5f)));
            vein.OnPointerUp(Pointer(Vector2.Lerp(start, end, 0.5f)));
            Assert.AreEqual(restScale, marker.localScale, "Letting go puts it back");
            Assert.AreEqual(restColor, graphic.color);

            vein.Show(target, 70f, 90f, 0.95f, null, null);
            vein.OnPointerDown(Pointer(start));
            Assert.AreNotEqual(restScale, marker.localScale);
            vein.Hide();
            Assert.AreEqual(restScale, marker.localScale, "Hiding while held restores it for the next vein");
            Assert.AreEqual(restColor, graphic.color);

            vein.Show(target, 70f, 90f, 0.95f, null, null);
            vein.OnPointerDown(Pointer(start));
            vein.Show(target, 70f, 90f, 0.95f, null, null);
            Assert.AreEqual(restScale, marker.localScale, "A pooled reuse starts plain");
            vein.Hide();
        }

        // The anchors come from no pool the tap targets use, and stay two however many veins a run has.
        [UnityTest]
        public IEnumerator A_run_reuses_its_two_vein_anchors()
        {
            BeginRun(targets: 4, maxMisses: 99, veinEvery: 1);
            PressStart();
            for (int i = 0; i < 4; i++)
            {
                yield return WaitForLiveVein();
                VeinView().OnPointerDown(Pointer(VeinView().StartPosition));
                VeinView().OnPointerUp(Pointer(VeinView().StartPosition));
                yield return null;
            }

            Assert.AreEqual(2, UnityEngine.Object.FindObjectsByType<ActiveMiningTargetPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
        }

        [UnityTest]
        public IEnumerator A_vein_has_the_screen_to_itself()
        {
            BeginRun(targets: 2, maxMisses: 3, decoyChance: 2f, veinEvery: 2);
            PressStart();
            yield return WaitForLiveDecoy();
            Assert.IsFalse(VeinView().gameObject.activeSelf, "The first target is a tap");

            Private<Button>(LiveTarget(), "_button").onClick.Invoke();
            yield return WaitForLiveVein();

            Assert.IsTrue(VeinView().gameObject.activeSelf);
            Assert.IsNull(LiveTarget(), "No target shows beside it");
            Assert.IsNull(LiveDecoy(), "Nor a decoy");
        }

        [UnityTest]
        public IEnumerator Target_clones_and_the_hud_draw_above_the_vein_view_and_the_miss_area_below()
        {
            yield return null;

            var vein = VeinView().transform;
            Assert.Greater(vein.GetSiblingIndex(), Private<Button>(_view, "_missAreaButton").transform.GetSiblingIndex());
            Assert.Less(vein.GetSiblingIndex(), Private<Text>(_view, "_progressText").transform.GetSiblingIndex());
            Assert.Less(vein.GetSiblingIndex(), Private<Text>(_view, "_gradeText").transform.GetSiblingIndex());
        }

        [UnityTest]
        public IEnumerator The_asteroid_slows_for_a_vein_and_spins_back_up_when_it_resolves()
        {
            Time.captureDeltaTime = 0.05f;
            BeginRun(targets: 2, maxMisses: 3, veinEvery: 1, spinDegrees: 40f, spawnGapSeconds: 5f);
            PressStart();
            yield return WaitForLiveVein();
            var stage = Private<ActiveMiningAsteroidStage>(_view, "_stage");

            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(5f, stage.RotationSpeed, 0.001f, "The asteroid eases down while the vein is live");

            VeinView().OnPointerDown(Pointer(VeinView().StartPosition));
            VeinView().OnPointerUp(Pointer(VeinView().StartPosition));

            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(40f, stage.RotationSpeed, 0.001f, "and picks up again once it resolves");
        }

        [UnityTest]
        public IEnumerator A_run_that_ends_on_a_vein_restores_the_spin_and_hides_the_line()
        {
            Time.captureDeltaTime = 0.05f;
            BeginRun(targets: 1, maxMisses: 3, veinEvery: 1, spinDegrees: 40f);
            PressStart();
            yield return WaitForLiveVein();
            var stage = Private<ActiveMiningAsteroidStage>(_view, "_stage");
            yield return new WaitForSeconds(0.3f);

            VeinView().OnPointerDown(Pointer(VeinView().StartPosition));
            VeinView().OnDrag(Pointer(VeinView().EndPosition));
            Assert.AreEqual(ActiveMiningStage.Success, Session().Stage);
            Assert.IsFalse(VeinView().gameObject.activeSelf);

            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(40f, stage.RotationSpeed, 0.001f);
        }

        [UnityTest]
        public IEnumerator A_completed_vein_plays_the_hit_sound_effect_and_shows_its_grade_clear_of_the_end_marker()
        {
            yield return BeginVein(targets: 2);
            var     vein = VeinView();
            Vector2 end  = vein.EndPosition;

            vein.OnPointerDown(Pointer(vein.StartPosition));
            vein.OnDrag(Pointer(end));

            var label  = Private<Text>(_view, "_gradeText");
            var hitVfx = Private<List<GameObject>>(_view, "_hitVfx");
            CollectionAssert.AreEqual(new[] { SfxId.ActiveMiningTap }, _audio.Played);
            Assert.AreEqual(1, hitVfx.Count);
            Assert.Less(Vector3.Distance(Private<ActiveMiningTargetPoint>(_view, "_veinEnd").transform.position, hitVfx[0].transform.position), 0.01f);
            Assert.AreEqual("Perfect", GradeText());
            Assert.AreEqual(new Color(1f, 0.84f, 0.3f), label.color, "Gold, like a tap");
            float clearance = ((RectTransform)Private<ActiveMiningTargetView>(_view, "_targetTemplate").transform).rect.height
                * _view.GetComponent<Canvas>().scaleFactor;
            Assert.AreEqual(clearance, Vector2.Distance(end, label.transform.position), 0.5f, "A marker's height from the end, not under the fingertip");
        }

        [UnityTest]
        public IEnumerator A_vein_finished_after_the_good_cut_off_shows_late()
        {
            Time.captureDeltaTime = 0.05f;
            yield return BeginVein(targets: 2); // Perfect by 1.1 s, Good by 1.35 s
            var target = VeinView().Target;
            yield return WaitUntil(() => target.ElapsedSeconds > 1.4f, "the Good cut-off to pass");

            VeinView().OnPointerDown(Pointer(VeinView().StartPosition));
            VeinView().OnDrag(Pointer(VeinView().EndPosition));

            Assert.AreEqual("Late", GradeText());
            Assert.AreEqual(Color.white, Private<Text>(_view, "_gradeText").color);
        }

        [UnityTest]
        public IEnumerator A_failed_vein_plays_the_miss_feedback()
        {
            Time.captureDeltaTime = 0.05f;
            yield return BeginVein(targets: 2);
            var     vein  = VeinView();
            Vector2 start = vein.StartPosition;
            var     clone = StageClone();
            var     rest  = clone.transform.localPosition;

            vein.OnPointerDown(Pointer(start));
            vein.OnPointerUp(Pointer(start));
            yield return null;

            CollectionAssert.AreEqual(new[] { SfxId.ActiveMiningMiss }, _audio.Played);
            Assert.AreEqual("Miss", GradeText());
            Assert.Less(Vector2.Distance(start, Private<Text>(_view, "_gradeText").transform.position), 0.01f, "Where the vein began");
            Assert.AreEqual(0, Private<List<GameObject>>(_view, "_hitVfx").Count, "A miss is not a hit");
            Assert.Greater(Vector3.Distance(rest, clone.transform.localPosition), 0f, "The rock is shaking");
        }

        [UnityTest]
        public IEnumerator A_failed_vein_on_the_final_miss_plays_only_the_fail_sound()
        {
            yield return BeginVein(targets: 2, maxMisses: 1);
            var vein = VeinView();

            vein.OnPointerDown(Pointer(vein.StartPosition));
            vein.OnPointerUp(Pointer(vein.StartPosition));

            Assert.AreEqual(ActiveMiningStage.Failed, Session().Stage);
            CollectionAssert.AreEqual(new[] { SfxId.ActiveMiningFail }, _audio.Played);
            Assert.AreEqual("Miss", GradeText(), "though the label still says so");
            Assert.IsFalse(vein.gameObject.activeSelf, "The line goes with the HUD");
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
        public IEnumerator A_perfect_hit_shows_gold()
        {
            PressStart();
            yield return TapNextTarget();

            Assert.AreEqual("Perfect", GradeText());
            Assert.AreEqual(new Color(1f, 0.84f, 0.3f), Private<Text>(_view, "_gradeText").color);
        }

        [UnityTest]
        public IEnumerator A_hit_after_the_perfect_cut_off_shows_good()
        {
            BeginRun(targets: 1, maxMisses: 3, perfectBySeconds: -1f);
            PressStart();
            yield return TapNextTarget();

            Assert.AreEqual("Good", GradeText());
            Assert.AreEqual(new Color(0.42f, 0.83f, 0.46f), Private<Text>(_view, "_gradeText").color, "Green");
        }

        [UnityTest]
        public IEnumerator A_hit_after_the_good_cut_off_shows_late()
        {
            BeginRun(targets: 1, maxMisses: 3, perfectBySeconds: -2f, goodBySeconds: -1f);
            PressStart();
            yield return TapNextTarget();

            Assert.AreEqual("Late", GradeText());
            Assert.AreEqual(Color.white, Private<Text>(_view, "_gradeText").color);
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

        // The same marker image and ring serve both kinds, so the look is what tells them apart.
        [UnityTest]
        public IEnumerator A_decoy_shows_a_red_x_and_a_target_the_normal_marker()
        {
            BeginRun(targets: 2, maxMisses: 3, decoyChance: 2f);
            PressStart();
            yield return WaitForLiveDecoy();

            var decoy    = LiveDecoy();
            var target   = LiveTarget();
            var template = Private<ActiveMiningTargetView>(_view, "_targetTemplate");
            var decoyArt = Private<Sprite>(decoy, "_decoySprite");

            var decoyMarker = Private<Image>(decoy, "_marker");
            Assert.AreSame(decoyArt, decoyMarker.sprite);
            AssertRed(decoyMarker.color);
            AssertRed(Private<RectTransform>(decoy, "_ring").GetComponent<Image>().color);

            var targetMarker = Private<Image>(target, "_marker");
            Assert.AreNotSame(decoyArt, targetMarker.sprite);
            Assert.AreSame(Private<Image>(template, "_marker").sprite, targetMarker.sprite);
            Assert.AreEqual(Private<Image>(template, "_marker").color, targetMarker.color);
            Assert.AreEqual(Private<RectTransform>(template, "_ring").GetComponent<Image>().color,
                Private<RectTransform>(target, "_ring").GetComponent<Image>().color);
        }

        // Two markers never fit on a rock this small without their tap areas overlapping, so the decoy gives way.
        [UnityTest]
        public IEnumerator A_decoy_with_no_clear_spot_is_not_shown_and_costs_nothing()
        {
            SpawnRock(0.002f);

            BeginRun(targets: 2, maxMisses: 3, decoyChance: 2f, roomyRock: false);
            PressStart();
            yield return WaitForLiveTarget();
            yield return null;

            Assert.IsNull(LiveDecoy(), "No decoy is on screen");
            Assert.IsFalse(Session().LiveTargets.Any(t => t.Kind == ActiveMiningTargetKind.Decoy), "Nor in the session");
            Assert.AreEqual("Misses: 0/3", ErrorText());

            yield return TapNextTarget();
            Assert.AreEqual(ActiveMiningStage.InProgress, Session().Stage);
            Assert.AreEqual("1/2", ProgressText());
        }

        [UnityTest]
        public IEnumerator A_target_placed_over_a_live_decoy_dismisses_it_at_no_cost()
        {
            BeginRun(targets: 2, maxMisses: 3, decoyChance: 2f);
            PressStart();
            yield return WaitForLiveDecoy();
            var decoy = LiveDecoy();

            InvokePrivate(_view, "DismissDecoysNear", (Vector2)(Vector3)decoy.transform.position + new Vector2(10f, 10f));

            Assert.IsNull(decoy.Target, "The decoy's view is free");
            Assert.IsNull(LiveDecoy());
            Assert.IsFalse(Session().LiveTargets.Any(t => t.Kind == ActiveMiningTargetKind.Decoy));
            Assert.AreEqual("Misses: 0/3", ErrorText());
        }

        [UnityTest]
        public IEnumerator A_decoy_clear_of_a_new_target_is_left_alone()
        {
            BeginRun(targets: 2, maxMisses: 3, decoyChance: 2f);
            PressStart();
            yield return WaitForLiveDecoy();
            var decoy = LiveDecoy();

            InvokePrivate(_view, "DismissDecoysNear", (Vector2)(Vector3)decoy.transform.position + new Vector2(1000f, 0f));

            Assert.IsNotNull(decoy.Target);
        }

        [UnityTest]
        public IEnumerator A_decoy_is_drawn_below_a_real_target()
        {
            BeginRun(targets: 2, maxMisses: 3, decoyChance: 2f);
            PressStart();
            yield return WaitForLiveDecoy();

            Assert.Less(LiveDecoy().transform.GetSiblingIndex(), LiveTarget().transform.GetSiblingIndex());
        }

        // Views are pooled, so a view that showed a decoy is next handed a target.
        [UnityTest]
        public IEnumerator A_pooled_view_that_showed_a_decoy_shows_the_normal_look_for_a_target()
        {
            BeginRun(targets: 2, maxMisses: 3, decoyChance: 2f);
            PressStart();
            yield return WaitForLiveDecoy();
            var view     = LiveDecoy();
            var template = Private<ActiveMiningTargetView>(_view, "_targetTemplate");

            view.Hide();
            view.Show(new ActiveMiningTarget(99, ActiveMiningTargetKind.Normal, 1f), _ => { });

            var marker = Private<Image>(view, "_marker");
            Assert.AreSame(Private<Image>(template, "_marker").sprite, marker.sprite);
            Assert.AreEqual(Private<Image>(template, "_marker").color, marker.color);
            Assert.AreEqual(Private<RectTransform>(template, "_ring").GetComponent<Image>().color,
                Private<RectTransform>(view, "_ring").GetComponent<Image>().color);
        }

        [UnityTest]
        public IEnumerator Tapping_a_decoy_is_answered_as_a_miss_with_no_hit_effect()
        {
            BeginRun(targets: 2, maxMisses: 3, decoyChance: 2f);
            PressStart();
            yield return WaitForLiveDecoy();
            var     decoy = LiveDecoy();
            Vector3 at    = decoy.transform.position;

            Private<Button>(decoy, "_button").onClick.Invoke();

            var label = Private<Text>(_view, "_gradeText");
            Assert.AreEqual("Misses: 1/3", ErrorText());
            Assert.AreEqual("0/2", ProgressText());
            Assert.AreEqual("Miss", GradeText());
            Assert.Less(Vector3.Distance(at, label.transform.position), 0.01f, "The label is where the decoy was");
            CollectionAssert.AreEqual(new[] { SfxId.ActiveMiningMiss }, _audio.Played);
            Assert.AreEqual(0, Private<List<GameObject>>(_view, "_hitVfx").Count, "A decoy is not a hit");
            Assert.IsNull(decoy.Target, "The decoy goes");
            Assert.IsNotNull(LiveTarget(), "The target stays");
        }

        [UnityTest]
        public IEnumerator An_ignored_decoy_closes_and_goes_away_at_no_cost()
        {
            BeginRun(targets: 2, maxMisses: 3, ringSeconds: 1f, decoyChance: 2f, spawnGapSeconds: 5f);
            PressStart();
            yield return WaitForLiveDecoy();
            var decoy = LiveDecoy().Target;
            Private<Button>(LiveTarget(), "_button").onClick.Invoke();

            yield return WaitUntil(() => decoy.Progress >= 1f, "the decoy's ring to close");

            Assert.IsNull(LiveDecoy(), "Its marker goes away");
            Assert.AreEqual("Misses: 0/3", ErrorText());
            CollectionAssert.AreEqual(new[] { SfxId.ActiveMiningTap }, _audio.Played, "Only the hit made a sound");
            Assert.AreEqual("Perfect", GradeText(), "and no miss label followed it");
        }

        [UnityTest]
        public IEnumerator A_wrong_tap_labels_the_target_not_a_decoy_beside_it()
        {
            BeginRun(targets: 2, maxMisses: 3, decoyChance: 2f);
            PressStart();
            yield return WaitForLiveDecoy();
            Vector2 at        = LiveTarget().transform.position;
            float   clearance = ((RectTransform)LiveTarget().transform).rect.height * _view.GetComponent<Canvas>().scaleFactor;

            TapMissArea();

            var label = Private<Text>(_view, "_gradeText");
            Assert.AreEqual(clearance, Vector2.Distance(at, label.transform.position), 0.5f);
        }

        // Reuse of a freed pool slot puts the newer target ahead of the older one in pool order.
        [UnityTest]
        public IEnumerator A_wrong_tap_labels_the_oldest_live_target()
        {
            BeginRun(targets: 4, maxMisses: 3, liveTargets: 2);
            PressStart();
            yield return WaitUntil(() => LiveTargetViews().Count == 2, "both targets to be live");
            Private<Button>(LiveTargetViews()[0], "_button").onClick.Invoke();
            yield return WaitUntil(() => LiveTargetViews().Count == 2, "a replacement to be live");

            var views  = LiveTargetViews();
            var oldest = views[0];
            var newer  = views[1];
            var slots  = (IList)typeof(ActiveMiningMinigameView).GetField("_slots", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_view);
            var firstInPool = (ActiveMiningTargetView)slots[0].GetType().GetField("View").GetValue(slots[0]);
            Assert.AreSame(newer, firstInPool, "The newer target comes first in pool order");

            float clearance = ((RectTransform)oldest.transform).rect.height * _view.GetComponent<Canvas>().scaleFactor;
            TapMissArea();

            var label = Private<Text>(_view, "_gradeText");
            Assert.AreEqual(clearance, Vector2.Distance(oldest.transform.position, label.transform.position), 0.5f);
        }

        // Where the rules gain a second line, it has to fit the box and stay clear of the start prompt.
        [UnityTest]
        public IEnumerator The_rules_for_a_tier_with_decoys_fit_on_screen_clear_of_the_start_prompt()
        {
            var tierTwo = new ActiveMiningRunSettings(2,
                new ActiveMiningTierSettings(10, 1.3f, 0.75f, 1.05f, 3, 0.25f),
                perfectShare: 1f, goodShare: 0.8f, lateShare: 0.6f, spawnGapSeconds: 0.25f, missGraceSeconds: 0.2f);
            StringAssert.Contains("decoys", ActiveMiningRulesText.For(tierTwo));
            return RulesFitOnScreen(tierTwo);
        }

        // The longest the rules get: every challenge named on one line that has to wrap.
        [UnityTest]
        public IEnumerator The_rules_for_tier_5_fit_on_screen_clear_of_the_start_prompt()
        {
            var tierFive = new ActiveMiningRunSettings(5,
                new ActiveMiningTierSettings(16, 0.9f, 0.6f, 0.75f, 3, 0.35f, 2, 30f, 45f, 4),
                perfectShare: 1f, goodShare: 0.8f, lateShare: 0.6f, spawnGapSeconds: 0.25f, missGraceSeconds: 0.2f);
            StringAssert.Contains("dragging along lines", ActiveMiningRulesText.For(tierFive));
            return RulesFitOnScreen(tierFive);
        }

        private IEnumerator RulesFitOnScreen(ActiveMiningRunSettings settings)
        {
            var rules       = Private<Text>(_view, "_rulesText");
            var startPrompt = Private<Button>(_view, "_startButton").GetComponentInChildren<Text>();
            var mineralType = Private<Text>(_view, "_mineralTypeText");
            rules.text = ActiveMiningRulesText.For(settings);
            yield return null;
            Canvas.ForceUpdateCanvases();

            Assert.LessOrEqual(rules.preferredHeight, rules.rectTransform.rect.height, "The text is not cut off by its box");

            Rect box    = WorldRect(rules.rectTransform);
            Rect screen = new Rect(0f, 0f, Screen.width, Screen.height);
            Assert.IsTrue(screen.Contains(box.min) && screen.Contains(box.max), $"{box} is within the screen {screen}");
            Assert.IsFalse(box.Overlaps(WorldRect(mineralType.rectTransform)), "It clears the mineral type text");
            Assert.IsFalse(box.Overlaps(RenderedTextRect(startPrompt)), "It clears the start prompt");
        }

        // The second target joins a ring that is a share of the way round, so the older one is the lower id.
        [UnityTest]
        public IEnumerator Two_live_targets_can_be_tapped_in_either_order()
        {
            BeginRun(targets: 2, maxMisses: 3, liveTargets: 2);
            PressStart();
            yield return WaitUntil(() => LiveTargetViews().Count == 2, "both targets to be live");
            var views = LiveTargetViews();
            var older = views[0];
            var newer = views[1];
            Assert.Less(older.Target.Id, newer.Target.Id);

            Private<Button>(newer, "_button").onClick.Invoke();

            Assert.IsNull(newer.Target, "The tapped marker hides");
            Assert.AreSame(older, LiveTarget(), "The other stays");
            Assert.AreEqual("1/2", ProgressText());

            Private<Button>(older, "_button").onClick.Invoke();

            Assert.IsNull(older.Target);
            Assert.IsNull(LiveTarget());
            Assert.AreEqual("2/2", ProgressText());
            Assert.AreEqual("Misses: 0/3", ErrorText());
            Assert.AreEqual((2, 0, ActiveMiningStage.Success), (Session().PerfectCount, Session().ErrorCount, Session().Stage));
        }

        private static List<ActiveMiningTargetView> LiveTargetViews() =>
            UnityEngine.Object.FindObjectsByType<ActiveMiningTargetView>(FindObjectsSortMode.None)
                .Where(view => view.Target != null && view.Target.Kind == ActiveMiningTargetKind.Normal)
                .OrderBy(view => view.Target.Id)
                .ToList();

        private static void AssertRed(Color color)
        {
            Assert.Greater(color.r, 0.8f);
            Assert.Less(color.g, 0.4f);
            Assert.Less(color.b, 0.4f);
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

        // The dev tier picker is for runs that began in Planet, which is what supplies its tier lookup. A
        // tier N run here has N targets, so a run's length shows which tier it played.
        [UnityTest]
        public IEnumerator A_picked_tier_sets_the_difficulty_of_the_run_that_starts_next()
        {
            OfferTierPicker();
            Assert.IsNotNull(_view.GetComponent<ActiveMiningDevTierPicker>(), "The picker is on offer before the run starts");

            PickTier(3);
            PressStart();
            yield return WaitForLiveTarget();

            Assert.AreEqual("0/3", ProgressText());
            Assert.AreEqual(3, _handoff.Settings.TierNumber);
            Assert.AreEqual(80, _handoff.FullQuantity, "The asteroid and what it pays are unchanged");
            Assert.IsTrue(_view.GetComponent<ActiveMiningDevTierPicker>() == null, "The picker goes once the run starts");
        }

        [UnityTest]
        public IEnumerator Picking_a_tier_shows_a_real_asteroid_of_that_tier()
        {
            OfferTierPicker();
            var rocks = Private<AsteroidDefinition[]>(_view, "_devAsteroidsByTier");
            Assert.AreEqual("Asteroid_Palladium", rocks[3].name, "The scene wires Tier 4 to Palladium");
            Assert.IsNotNull(rocks[3].ModelPrefab);
            string mineral = Private<Text>(_view, "_mineralTypeText").text;

            PickTier(4);
            yield return null;

            Assert.AreEqual(rocks[3].ModelPrefab.name + "(Clone)", StageClone().name);
            Assert.AreEqual(80, _handoff.FullQuantity, "What the run pays is unchanged");
            Assert.AreSame(_definition, _handoff.Definition);
            Assert.AreEqual(mineral, Private<Text>(_view, "_mineralTypeText").text);
        }

        // IMGUI doesn't block uGUI, so a tap on a tier button reaches the Start button too.
        [UnityTest]
        public IEnumerator A_press_on_the_tier_picker_does_not_start_the_run()
        {
            OfferTierPicker();
            var picker = _view.GetComponent<ActiveMiningDevTierPicker>();
            typeof(ActiveMiningDevTierPicker).GetProperty("IsPressed").SetValue(picker, true);

            PressStart();
            yield return null;

            Assert.IsNull(Session(), "No run began");
            Assert.IsTrue(Private<GameObject>(_view, "_preGamePanel").activeSelf);

            typeof(ActiveMiningDevTierPicker).GetProperty("IsPressed").SetValue(picker, false);
            PressStart();
            yield return null;

            Assert.IsNotNull(Session(), "Once the pointer is up, Start works again");
        }

        [UnityTest]
        public IEnumerator A_run_that_did_not_begin_in_planet_has_no_tier_picker()
        {
            yield return null;

            Assert.IsTrue(_view.GetComponent<ActiveMiningDevTierPicker>() == null);
        }

        private void OfferTierPicker()
        {
            _handoff.DevSettingsForTier = tier => new ActiveMiningRunSettings(tier,
                new ActiveMiningTierSettings(tier, 5f, 4f, 4.5f, 3),
                perfectShare: 1f, goodShare: 0.8f, lateShare: 0.6f, spawnGapSeconds: 0f, missGraceSeconds: 0f);
            InvokePrivate(_view, "ShowTierPicker");
        }

        private void PickTier(int tier) => InvokePrivate(_view, "OnTierPicked", tier);

        // A decoyChance of 2 puts a decoy beside every target.
        private void BeginRun(int targets, int maxMisses, float ringSeconds = 5f, float perfectBySeconds = 4f, float goodBySeconds = 4.5f,
            float decoyChance = 0f, float spawnGapSeconds = 0f, int liveTargets = 1, float spinDegrees = 0f, bool roomyRock = true, int veinEvery = 0,
            float veinSpinDegrees = 5f, float veinDragTolerance = 90f)
        {
            // The stand-in rock is too big for the camera to leave a decoy a clear spot, so a run with decoys gets one that does.
            // In the test window, a rock half this size turns away about one decoy in thirteen, when its target lands near the middle.
            if (decoyChance > 0f && roomyRock && _view != null)
            {
                SpawnRock(0.2f);
                if (spinDegrees <= 0f) spinDegrees = 5f;
            }
            var tier = spinDegrees > 0f
                ? new ActiveMiningTierSettings(targets, ringSeconds, perfectBySeconds, goodBySeconds, maxMisses, decoyChance, liveTargets, spinDegrees, spinDegrees, veinEvery)
                : new ActiveMiningTierSettings(targets, ringSeconds, perfectBySeconds, goodBySeconds, maxMisses, decoyChance, liveTargets, veinEvery: veinEvery);
            var settings = new ActiveMiningRunSettings(1, tier,
                perfectShare: 1f, goodShare: 0.8f, lateShare: 0.6f, spawnGapSeconds: spawnGapSeconds, missGraceSeconds: 0f, veinRingSeconds: ringSeconds,
                veinSpinDegrees: veinSpinDegrees, veinDragTolerance: veinDragTolerance);
            _handoff.Begin("earth", "Iron#1", _definition, 80, settings);
        }

        // Every target a vein, the first one live on the stand-in rock.
        private IEnumerator BeginVein(int targets, int maxMisses = 3, float veinDragTolerance = 90f)
        {
            BeginRun(targets, maxMisses, veinEvery: 1, veinDragTolerance: veinDragTolerance);
            PressStart();
            yield return WaitForLiveVein();
        }

        private ActiveMiningVeinView VeinView() => Private<ActiveMiningVeinView>(_view, "_veinView");

        private IEnumerator WaitForLiveVein() => WaitUntil(() => VeinView().Target != null, "a vein to spawn");

        private static PointerEventData Pointer(Vector2 position, int id = 0) =>
            new PointerEventData(EventSystem.current) { position = position, pointerId = id };

        private void SpawnRock(float diameter)
        {
            var model = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            model.transform.localScale = Vector3.one * diameter;
            SetPrivate(_definition, "_modelPrefab", model);
            Private<ActiveMiningAsteroidStage>(_view, "_stage").SpawnClone(_definition);
            UnityEngine.Object.Destroy(model);
        }

        private void PressStart()   => Private<Button>(_view, "_startButton").onClick.Invoke();
        private void TapMissArea()  => Private<Button>(_view, "_missAreaButton").onClick.Invoke();

        private GameObject StageClone() => Private<ActiveMiningAsteroidStage>(_view, "_stage").StageClone;

        private ActiveMiningSession Session() => Private<ActiveMiningSessionRunner>(_view, "_runner").Session;

        // The speed comes from the run's settings, which the stage reads when Start is pressed.
        private void SpinAsteroid(Vector3 axis) =>
            SetPrivate(Private<ActiveMiningAsteroidStage>(_view, "_stage"), "_rotationAxis", axis);

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

        private IEnumerator WaitForLiveDecoy() => WaitUntil(() => LiveDecoy() != null, "a decoy to spawn");

        private static ActiveMiningTargetView LiveTarget() => LiveView(ActiveMiningTargetKind.Normal);

        private static ActiveMiningTargetView LiveDecoy() => LiveView(ActiveMiningTargetKind.Decoy);

        private static ActiveMiningTargetView LiveView(ActiveMiningTargetKind kind) =>
            UnityEngine.Object.FindObjectsByType<ActiveMiningTargetView>(FindObjectsSortMode.None)
                .FirstOrDefault(view => view.Target != null && view.Target.Kind == kind);

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

        private static void InvokePrivate(object target, string name, params object[] args)
        {
            var method = target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, $"{target.GetType().Name} has no {name}");
            method.Invoke(target, args);
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
