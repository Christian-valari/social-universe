using Unity.Collections;
using Unity.Netcode;

namespace SocialUniverse.Net
{
    // Replicated player marker on a planet. Until a real avatar model is
    // acquired (see architecture §1, "models to acquire") the prefab is a
    // simple marker mesh; this component replicates identity, and
    // PlayerSyncController (same prefab) replicates position on the sphere.
    //
    // Scene setup: lives on the NetworkManager's Player Prefab together with
    // a NetworkObject and PlayerSyncController.
    public class NetworkPlayer : NetworkBehaviour
    {
        private readonly NetworkVariable<FixedString64Bytes> _playerId = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<FixedString64Bytes> _displayName = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public string PlayerId    => _playerId.Value.ToString();
        public string DisplayName => _displayName.Value.ToString();

        // Called once by the owning client after spawn (PlayerSyncController
        // wires this from the session identity).
        public void SetIdentity(string playerId, string displayName)
        {
            if (!IsOwner) return;
            _playerId.Value    = playerId ?? "";
            _displayName.Value = displayName ?? "";
        }
    }
}
