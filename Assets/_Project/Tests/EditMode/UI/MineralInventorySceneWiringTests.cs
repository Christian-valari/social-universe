using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    // MineralInventoryView's sale popup (_saleModal) was placed in the Planet scene by a script
    // (2026-10-02). A dropped reference leaves Sell dead with no error, so open the scene and check.
    public class MineralInventorySceneWiringTests
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
        public void Every_MineralInventoryView_serialized_reference_is_wired()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), $"Could not open {ScenePath}");

            var view = Object.FindFirstObjectByType<MineralInventoryView>(FindObjectsInactive.Include);
            Assert.IsNotNull(view, "No MineralInventoryView component in the Planet scene");

            var prop = new SerializedObject(view).GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.propertyType != SerializedPropertyType.ObjectReference || prop.name == "m_Script") continue;
                Assert.IsNotNull(prop.objectReferenceValue,
                    $"MineralInventoryView.{prop.name} is not wired in {ScenePath} ({{fileID: 0}})");
            }
        }
    }
}
