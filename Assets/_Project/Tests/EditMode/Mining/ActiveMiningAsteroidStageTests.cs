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
