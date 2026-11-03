using System;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using SocialUniverse.UI;
using Object = UnityEngine.Object;

namespace SocialUniverse.Tests
{
    // The confirmation and message popups share one style (2026-10-23): TextMeshPro throughout and a
    // window sized for a phone. On screen they used to range from ~56% of the canvas width (Settings,
    // Land purchase, Fuel) to ~80% (Mining prompt), some sized by layout and some by transform scale,
    // so check each popup's window as it appears.
    public class PopupStyleTests
    {
        private const string Planet       = "Assets/Scenes/Planet.unity";
        private const string LandBuilding = "Assets/Scenes/LandBuilding.unity";

        // Share of its canvas's width a centred window must cover; the old layouts covered ~0.4–0.5.
        private const float MinWindowShare = 0.6f;

        private SceneSetup[] _originalSetup;

        [SetUp]
        public void SetUp() => _originalSetup = EditorSceneManager.GetSceneManagerSetup();

        [TearDown]
        public void TearDown()
        {
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
        [TestCase(Planet,       typeof(TransactionModal),       "_window")]
        [TestCase(LandBuilding, typeof(HexBuildPopup),          "_window")]
        public void Every_scene_popup_uses_TextMeshPro_and_is_sized_for_a_phone(string scenePath, Type popupType, string field)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var popups = Object.FindObjectsByType(popupType, FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.IsNotEmpty(popups, $"No {popupType.Name} in {scenePath}");

            foreach (var popup in popups)
            {
                var window = new SerializedObject(popup).FindProperty(field).objectReferenceValue as RectTransform;
                Assert.IsNotNull(window, $"{popupType.Name}.{field} is not assigned in {scenePath}");
                AssertStyled(window, $"{popupType.Name}.{field} on '{((Component)popup).gameObject.name}'");
            }
        }

        [TestCase("Assets/Prefabs/UI/TransactionModal.prefab")]
        [TestCase("Assets/_Project/Prefabs/UI/Resources/QuitDialog.prefab")]
        [TestCase("Assets/_Project/Prefabs/UI/Resources/SyncRetryDialog.prefab")]
        [TestCase("Assets/_Project/Prefabs/UI/Resources/Toast.prefab")]
        public void Every_popup_prefab_uses_TextMeshPro(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab, $"Could not load {prefabPath}");
            CollectionAssert.IsEmpty(prefab.GetComponentsInChildren<Text>(true), $"{prefabPath} still has legacy Text");
        }

        private static void AssertStyled(RectTransform window, string what)
        {
            CollectionAssert.IsEmpty(window.GetComponentsInChildren<Text>(true), $"{what} still has legacy Text");

            // A window stretched across its parent (Tile info is a bottom sheet) is as wide as the screen.
            bool centred = Mathf.Approximately(window.anchorMin.x, window.anchorMax.x);
            if (!centred) return;

            // Canvases differ (Planet's is 1080 wide, LandBuilding's 800), so compare with the canvas;
            // and several popups are sized by transform scale (roots at 1.44), so measure on screen.
            var scaler = window.GetComponentInParent<CanvasScaler>(true);
            Assert.IsNotNull(scaler, $"{what} has no CanvasScaler above it");
            float onScreen = window.sizeDelta.x * ScaleBelowCanvas(window);
            Assert.GreaterOrEqual(onScreen, scaler.referenceResolution.x * MinWindowShare, $"{what} is narrower than a phone popup");
            Assert.LessOrEqual(onScreen, scaler.referenceResolution.x, $"{what} is wider than its canvas");
        }

        private static float ScaleBelowCanvas(Transform t)
        {
            float s = 1f;
            for (var x = t; x != null && x.GetComponent<Canvas>() == null; x = x.parent) s *= x.localScale.x;
            return s;
        }
    }
}
