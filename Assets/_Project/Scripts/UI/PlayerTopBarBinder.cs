using UnityEngine;
using VContainer;
using SocialUniverse.Config;
using SocialUniverse.Economy;
using SocialUniverse.Progression;

namespace SocialUniverse.UI
{
    // Host that binds a PlayerTopBarView in the LandBuilding scene. PlayerTopBarView is a passive
    // view (in the Planet scene HUDController is its host); LandBuilding has no HUDController, so
    // this component plays that role. Lives ON the PlayerTopBar prefab: it only activates where a
    // scope registers it via RegisterComponentInHierarchy (LandBuildingSceneScope). In the Planet
    // scene the same prefab carries this component but no scope registers it, so its [Inject] fields
    // stay null and Start() no-ops — HUDController still binds there as before.
    //
    // Coins live-update: LandBuildPaletteView pushes each PlaceBuild/PurchaseHexatile NewBalance
    // into this same Wallet. Name/avatar/stardust are a static snapshot — PlayerState/Wallet don't
    // survive the Planet -> LandBuilding scene swap, so LandBuildingSceneScope reconstructs them
    // from the LandBuildingHandoff and never mutates name/avatar/stardust afterward.
    [RequireComponent(typeof(PlayerTopBarView))]
    public class PlayerTopBarBinder : MonoBehaviour
    {
        [Inject] private Wallet           _wallet;
        [Inject] private PlayerState      _playerState;
        [Inject] private DatabaseRegistry _registry;

        private void Start()
        {
            // Null when this prefab is in a scene whose scope doesn't register this component
            // (e.g. the Planet HUD, where HUDController is the host instead).
            if (_wallet == null || _playerState == null) return;

            var bar = GetComponent<PlayerTopBarView>();
            if (bar != null) bar.Bind(_wallet, _playerState, _registry);
        }
    }
}
