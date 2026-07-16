using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    // The Auth scene has shipped a {fileID: 0} wiring regression before
    // (_forgotPasswordButton, found 2026-07-16). This opens the real scene
    // and asserts every AuthScreen serialized reference is wired.
    public class AuthSceneWiringTests
    {
        private const string ScenePath = "Assets/Scenes/Auth.unity";

        [Test]
        public void Every_AuthScreen_serialized_reference_is_wired()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), $"Could not open {ScenePath}");

            var screen = Object.FindFirstObjectByType<AuthScreen>(FindObjectsInactive.Include);
            Assert.IsNotNull(screen, "No AuthScreen component in the Auth scene");

            var so = new SerializedObject(screen);
            var prop = so.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (prop.name == "m_Script") continue;
                Assert.IsNotNull(prop.objectReferenceValue,
                    $"AuthScreen.{prop.name} is not wired in {ScenePath} ({{fileID: 0}})");
            }
        }
    }
}
