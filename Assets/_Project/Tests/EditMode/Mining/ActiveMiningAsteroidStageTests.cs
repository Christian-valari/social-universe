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
    }
}
