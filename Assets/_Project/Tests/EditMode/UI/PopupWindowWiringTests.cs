using System;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SocialUniverse.UI;
using Object = UnityEngine.Object;

namespace SocialUniverse.Tests
{
    // Every popup opens and closes with PopupTransition's pop & fade (2026-10-05), which scales the
    // popup's window. The windows were assigned in the scenes by a script; a missing one would make
    // that popup open without growing, so check each (TransactionModal is covered by its prefab test).
    public class PopupWindowWiringTests
    {
        private const string Planet       = "Assets/Scenes/Planet.unity";
        private const string LandBuilding = "Assets/Scenes/LandBuilding.unity";

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

        [TestCase(Planet,       typeof(MiningModePromptView),   "_window")]
        [TestCase(Planet,       typeof(MiningClaimRewardModal), "_window")]
        [TestCase(Planet,       typeof(LandPurchaseModal),      "_window")]
        [TestCase(Planet,       typeof(TileInfoModal),          "_window")]
        [TestCase(Planet,       typeof(DisplayNameModal),       "_window")]
        [TestCase(Planet,       typeof(AvatarSelectionModal),   "_window")]
        [TestCase(Planet,       typeof(EmailVerificationModal), "_window")]
        [TestCase(Planet,       typeof(SettingsPanel),          "_window")]
        [TestCase(Planet,       typeof(SettingsPanel),          "_logoutConfirmWindow")]
        [TestCase(Planet,       typeof(FuelPanel),              "_window")]
        [TestCase(LandBuilding, typeof(HexBuildPopup),          "_window")]
        public void Every_popup_has_its_window_assigned(string scenePath, Type popupType, string field)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), $"Could not open {scenePath}");

            // Some popup types have several instances (LandBuilding has a purchase and a remove
            // HexBuildPopup), so check every one.
            var popups = Object.FindObjectsByType(popupType, FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.IsNotEmpty(popups, $"No {popupType.Name} in {scenePath}");

            foreach (var popup in popups)
            {
                var prop = new SerializedObject(popup).FindProperty(field);
                Assert.IsNotNull(prop, $"{popupType.Name} has no serialized {field}");
                Assert.IsInstanceOf<RectTransform>(prop.objectReferenceValue,
                    $"{popupType.Name}.{field} on '{((Component)popup).gameObject.name}' is not assigned in {scenePath}");
            }
        }
    }
}
