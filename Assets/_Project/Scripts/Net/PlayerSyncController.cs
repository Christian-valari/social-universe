using SocialUniverse.Config;
using SocialUniverse.Core;
using Unity.Netcode;
using UnityEngine;

namespace SocialUniverse.Net
{
    // Syncs the local player's position on the planet sphere to other clients
    // and smooths remote markers. There is no walkable avatar yet, so the
    // local player's "position" is their camera focus point projected onto
    // the sphere surface — when a controllable avatar lands, only
    // SampleLocalPosition() changes.
    //
    // Scene setup: same prefab as NetworkPlayer (+ NetworkObject). Assumes the
    // planet is at the world origin (PlanetRoot convention from M1).
    public class PlayerSyncController : NetworkBehaviour
    {
        [SerializeField] private SocialConfig _config;
        [Tooltip("Sphere radius the marker sits on; match the hexasphere radius.")]
        [SerializeField] private float _surfaceRadius = 1.05f;
        [SerializeField] private float _remoteLerpSpeed = 5f;

        private readonly NetworkVariable<Vector3> _surfacePosition = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private Camera _camera;
        private float  _nextSendTime;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;

            _camera = Camera.main;
            var networkPlayer = GetComponent<NetworkPlayer>();
            if (networkPlayer != null)
            {
                // AuthenticationService identity travels via the session's
                // player properties; OwnerClientId is the NGO-level fallback.
                networkPlayer.SetIdentity(OwnerClientId.ToString(), $"Player {OwnerClientId}");
            }
            SULog.Info("PlayerSyncController: local player marker spawned", SULog.Channel.Net);
        }

        private void Update()
        {
            if (IsOwner) UpdateOwner();
            else         UpdateRemote();
        }

        private void UpdateOwner()
        {
            var position = SampleLocalPosition();
            transform.position = position;

            float interval = _config != null ? _config.PositionSyncIntervalSec : 0.5f;
            if (Time.time < _nextSendTime) return;
            _nextSendTime = Time.time + interval;
            _surfacePosition.Value = position;
        }

        private void UpdateRemote()
        {
            transform.position = Vector3.Lerp(
                transform.position, _surfacePosition.Value, Time.deltaTime * _remoteLerpSpeed);
        }

        private Vector3 SampleLocalPosition()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return transform.position;

            // Project the camera onto the sphere: the point of the planet the
            // player is looking at.
            var toCamera = _camera.transform.position;
            return toCamera.sqrMagnitude < 0.0001f
                ? Vector3.forward * _surfaceRadius
                : toCamera.normalized * _surfaceRadius;
        }
    }
}
