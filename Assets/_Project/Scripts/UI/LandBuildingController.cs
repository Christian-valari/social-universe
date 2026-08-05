using UnityEngine;
using UnityEngine.UI;
using VContainer;
using SocialUniverse.Core;

namespace SocialUniverse.UI
{
    // Owns the LandBuilding scene's Back button. The hex board itself is built by
    // LandBuildPaletteView (which injects PlotHexBoard); this controller only handles
    // navigation back to the planet the player came from.
    //
    // Plain MonoBehaviour: registered via RegisterComponentInHierarchy in LandBuildingSceneScope,
    // which injects [Inject] fields at container build time — before Unity's Start() runs.
    public class LandBuildingController : MonoBehaviour
    {
        [SerializeField] private Button _backButton;

        [Inject] private IObjectResolver _resolver;

        private void Start() => _backButton.onClick.AddListener(OnBack);

        private void OnBack() => _resolver.Resolve<LandBuildingState>().Finish();
    }
}
