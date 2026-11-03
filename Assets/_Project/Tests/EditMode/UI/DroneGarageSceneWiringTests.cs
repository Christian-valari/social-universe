using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    // DroneGarageView's buy button fields were renamed from _unlockSlotButton/_unlockSlotLabel
    // (2026-10-02) and rely on [FormerlySerializedAs] to keep the Planet scene's wiring. A dropped
    // reference leaves the garage's buy button dead with no error, so open the real scene and check.
    public class DroneGarageSceneWiringTests
    {
        private const string ScenePath = "Assets/Scenes/Planet.unity";

        private SceneSetup[] _originalSetup;

        [SetUp]
        public void SetUp() => _originalSetup = EditorSceneManager.GetSceneManagerSetup();

        [TearDown]
        public void TearDown()
        {
            // Same restore as AuthSceneWiringTests: put the developer's open scenes back, and skip it
            // in batchmode, where there is nothing to restore.
            if (_originalSetup != null && _originalSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(_originalSetup);
        }

        [Test]
        public void Every_DroneGarageView_serialized_reference_is_wired()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), $"Could not open {ScenePath}");

            var view = Object.FindFirstObjectByType<DroneGarageView>(FindObjectsInactive.Include);
            Assert.IsNotNull(view, "No DroneGarageView component in the Planet scene");

            var so = new SerializedObject(view);
            var prop = so.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (prop.name == "m_Script") continue;
                Assert.IsNotNull(prop.objectReferenceValue,
                    $"DroneGarageView.{prop.name} is not wired in {ScenePath} ({{fileID: 0}})");
            }
        }
    }
}
