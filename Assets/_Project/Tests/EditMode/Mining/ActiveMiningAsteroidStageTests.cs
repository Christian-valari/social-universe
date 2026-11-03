using System.Reflection;
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Mining;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class ActiveMiningAsteroidStageTests
    {
        private GameObject                 _stageGo;
        private ActiveMiningAsteroidStage  _stage;

        [SetUp]
        public void SetUp()
        {
            _stageGo = new GameObject("Stage");
            _stage   = _stageGo.AddComponent<ActiveMiningAsteroidStage>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_stageGo);

        [Test]
        public void SpawnClone_falls_back_to_a_primitive_sphere_when_no_model_prefab_is_set()
        {
            var def = ScriptableObject.CreateInstance<AsteroidDefinition>();

            GameObject clone = _stage.SpawnClone(def);

            Assert.IsNotNull(clone);
            Assert.IsNotNull(clone.GetComponent<Collider>());
            Assert.AreEqual(_stageGo.transform, clone.transform.parent);
            Assert.Greater(_stage.ColliderRadius, 0f);

            Object.DestroyImmediate(def);
        }

        [Test]
        public void SpawnClone_replaces_a_previous_clone_instead_of_stacking_them()
        {
            var def = ScriptableObject.CreateInstance<AsteroidDefinition>();

            var first  = _stage.SpawnClone(def);
            var second = _stage.SpawnClone(def);

            Assert.AreNotSame(first, second);
            Assert.IsTrue(first == null, "the previous clone must be destroyed, not left orphaned");

            Object.DestroyImmediate(def);
        }

        [Test]
        public void PredictPoint_returns_where_the_spin_carries_the_point()
        {
            var def = ScriptableObject.CreateInstance<AsteroidDefinition>();
            _stageGo.transform.position = new Vector3(1f, 2f, 3f);
            var center = _stage.SpawnClone(def).transform.position;
            SetSpin(Vector3.up, degreesPerSecond: 45f);

            Vector3 predicted = _stage.PredictPoint(center + Vector3.right * 2f, seconds: 2f);

            AssertNear(center + Vector3.back * 2f, predicted); // a quarter turn about up
            Assert.AreEqual(2f, Vector3.Distance(center, predicted), 0.001f);

            Object.DestroyImmediate(def);
        }

        // The same turn the stage applies each frame, on the spin SpawnClone actually rolled.
        [Test]
        public void PredictPoint_agrees_with_the_spin_the_stage_applies()
        {
            var def   = ScriptableObject.CreateInstance<AsteroidDefinition>();
            var clone = _stage.SpawnClone(def);
            var axis  = (Vector3)Field("_rotationAxis").GetValue(_stage);
            var speed = (float)Field("_rotationSpeed").GetValue(_stage);

            var surfacePoint = new GameObject("SurfacePoint").transform;
            surfacePoint.SetParent(clone.transform);
            surfacePoint.position = clone.transform.position + new Vector3(0.1f, 0.2f, -0.2f);

            Vector3 predicted = _stage.PredictPoint(surfacePoint.position, seconds: 3f);
            clone.transform.Rotate(axis, speed * 3f, Space.World);

            AssertNear(surfacePoint.position, predicted);

            Object.DestroyImmediate(def);
        }

        [Test]
        public void PredictPoint_leaves_the_point_alone_with_no_asteroid_to_spin_it()
        {
            var point = new Vector3(1f, 2f, 3f);

            AssertNear(point, _stage.PredictPoint(point, seconds: 2f));
        }

        [Test]
        public void Shake_jitters_the_clone_then_puts_it_back_exactly()
        {
            var def   = ScriptableObject.CreateInstance<AsteroidDefinition>();
            var clone = _stage.SpawnClone(def);
            clone.transform.localPosition = new Vector3(0.1f, 0.2f, 0.3f);
            var rest     = clone.transform.localPosition;
            var rotation = clone.transform.rotation;
            var distance = (float)Field("_shakeDistance").GetValue(_stage);

            _stage.Shake(0.5f);
            Advance(0.1f);
            float offset = Vector3.Distance(rest, clone.transform.localPosition);
            Assert.Greater(offset, 0f, "mid-shake the clone is off its resting place");
            Assert.LessOrEqual(offset, distance * 1.001f, "and only by a small jitter");

            for (int i = 0; i < 6; i++) Advance(0.1f);

            Assert.AreEqual(0f, (clone.transform.localPosition - rest).sqrMagnitude, "the clone is back exactly");
            Assert.AreEqual(rotation, clone.transform.rotation, "the shake never turns it");

            Object.DestroyImmediate(def);
        }

        [Test]
        public void A_shake_eases_out()
        {
            var def   = ScriptableObject.CreateInstance<AsteroidDefinition>();
            var clone    = _stage.SpawnClone(def);
            var rest     = clone.transform.localPosition;
            var distance = (float)Field("_shakeDistance").GetValue(_stage);

            _stage.Shake(0.5f);
            Advance(0.45f);

            Assert.Less(Vector3.Distance(rest, clone.transform.localPosition), distance * 0.5f);

            Object.DestroyImmediate(def);
        }

        // A miss that fails the run asks for 0.2 s and then 0.5 s on the same frame.
        [Test]
        public void A_longer_shake_carries_a_shorter_one_on_and_rests_where_the_first_began()
        {
            var def   = ScriptableObject.CreateInstance<AsteroidDefinition>();
            var clone = _stage.SpawnClone(def);
            clone.transform.localPosition = new Vector3(0.1f, 0.2f, 0.3f);
            var rest = clone.transform.localPosition;

            _stage.Shake(0.2f);
            Advance(0.05f);
            _stage.Shake(0.5f);
            Advance(0.3f);
            Assert.Greater(Vector3.Distance(rest, clone.transform.localPosition), 0f, "still shaking after the shorter one would have ended");

            Advance(0.3f);
            Assert.AreEqual(0f, (clone.transform.localPosition - rest).sqrMagnitude);

            Object.DestroyImmediate(def);
        }

        [Test]
        public void A_shorter_shake_does_not_cut_a_longer_one_short()
        {
            var def   = ScriptableObject.CreateInstance<AsteroidDefinition>();
            var clone = _stage.SpawnClone(def);
            var rest  = clone.transform.localPosition;

            _stage.Shake(0.5f);
            Advance(0.05f);
            _stage.Shake(0.2f);
            Advance(0.3f);

            Assert.Greater(Vector3.Distance(rest, clone.transform.localPosition), 0f);

            Object.DestroyImmediate(def);
        }

        [Test]
        public void SpawnClone_ends_a_shake_in_progress()
        {
            var def = ScriptableObject.CreateInstance<AsteroidDefinition>();
            _stage.SpawnClone(def);
            _stage.Shake(1f);
            Advance(0.1f);

            var replacement = _stage.SpawnClone(def);
            var rest        = replacement.transform.localPosition;
            Advance(0.1f);

            Assert.AreEqual(0f, (replacement.transform.localPosition - rest).sqrMagnitude, "the new clone sits still");

            Object.DestroyImmediate(def);
        }

        [Test]
        public void Shake_with_no_asteroid_does_nothing()
        {
            _stage.Shake(0.2f);

            Assert.DoesNotThrow(() => Advance(0.1f));
        }

        // A shake moves the clone for a moment; the spin carries a target on from where the clone rests.
        [Test]
        public void PredictPoint_turns_a_point_about_where_the_asteroid_rests_not_where_a_shake_has_it()
        {
            var def   = ScriptableObject.CreateInstance<AsteroidDefinition>();
            var clone = _stage.SpawnClone(def);
            SetSpin(Vector3.up, degreesPerSecond: 45f);
            Field("_shakeDistance").SetValue(_stage, 0.5f);
            Vector3 center = clone.transform.position;

            _stage.Shake(1f);
            Advance(0.05f);
            Vector3 shaken = clone.transform.position;
            Assert.Greater(Vector3.Distance(center, shaken), 0.1f, "the clone is well off its rest");

            Vector3 predicted = _stage.PredictPoint(shaken + Vector3.right * 2f, seconds: 2f);

            AssertNear(center + Vector3.back * 2f, predicted);

            Object.DestroyImmediate(def);
        }

        private void Advance(float seconds) =>
            typeof(ActiveMiningAsteroidStage).GetMethod("AdvanceShake", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_stage, new object[] { seconds });

        private void SetSpin(Vector3 axis, float degreesPerSecond)
        {
            Field("_rotationAxis").SetValue(_stage, axis);
            Field("_rotationSpeed").SetValue(_stage, degreesPerSecond);
        }

        private static FieldInfo Field(string name) =>
            typeof(ActiveMiningAsteroidStage).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

        private static void AssertNear(Vector3 expected, Vector3 actual) =>
            Assert.Less(Vector3.Distance(expected, actual), 0.001f, $"expected {expected}, got {actual}");
    }
}
