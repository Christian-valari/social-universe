using UnityEngine;
using UnityEngine.UI;
using VContainer;
using SocialUniverse.Economy;
using SocialUniverse.Mining;
using SocialUniverse.World;
using SocialUniverse.Progression;

namespace SocialUniverse.UI
{
    // Planet-scene HUD: surfaces currency and the player's current run state at a glance.
    public class HUDController : MonoBehaviour
    {
        [SerializeField] private CurrencyView _currency;
        [SerializeField] private Text _levelText;
        [SerializeField] private Text _fuelText;
        [SerializeField] private Text _miningStatusText;
        [SerializeField] private Text _landStatusText;

        [Inject] private Wallet _wallet;
        [Inject] private PlayerState _playerState;
        [Inject] private MiningController _mining;
        [Inject] private HexasphereManager _hexasphere;

        private void Start()
        {
            _currency.Bind(_wallet);

            _playerState.OnLevelChanged += SetLevel;
            _playerState.OnFuelChanged  += SetFuel;
            _mining.OnPhaseChanged      += _ => RefreshMiningStatus();

            SetLevel(_playerState.Level);
            SetFuel(_playerState.Fuel);
            RefreshMiningStatus();
            RefreshLandStatus();
        }

        private void OnDestroy()
        {
            _playerState.OnLevelChanged -= SetLevel;
            _playerState.OnFuelChanged  -= SetFuel;
        }

        private void Update()
        {
            // Cargo amount and owned-tile count have no change events — cheap to poll each frame.
            RefreshMiningStatus();
            RefreshLandStatus();
        }

        private void SetLevel(int level) => _levelText.text = $"Lv. {level}";

        private void SetFuel(float fuel) =>
            _fuelText.text = $"Fuel: {Mathf.CeilToInt(fuel)}/{Mathf.CeilToInt(_playerState.MaxFuel)}";

        private void RefreshMiningStatus()
        {
            var session = _mining.CurrentIdleSession;
            if (session != null)
            {
                _miningStatusText.text = session.Stage switch
                {
                    IdleMiningStage.Traveling    => $"Heading to {session.Asteroid.Definition.MineralType} asteroid...",
                    IdleMiningStage.Mining       => $"Mining {session.Asteroid.Definition.MineralType}: {Mathf.RoundToInt(session.MiningProgress01 * 100f)}%",
                    IdleMiningStage.ReadyToClaim => $"Tap the asteroid to claim! ({session.ClaimTapsRemaining} left)",
                    _                            => "Mining: —"
                };
                return;
            }

            var drone  = _mining.Drone;
            var target = _mining.CurrentTarget;

            if (drone == null)
            {
                _miningStatusText.text = "Mining: —";
                return;
            }

            string mineral = target?.Definition != null ? target.Definition.MineralType : "—";
            _miningStatusText.text = $"Mining {mineral}: {drone.CargoAmount}/{drone.Definition.CargoCap}";
        }

        private void RefreshLandStatus()
        {
            int owned = 0;
            foreach (var kv in _hexasphere.Tiles)
                if (kv.Value.State == TileState.OwnedByPlayer) owned++;

            _landStatusText.text = $"Tiles owned: {owned}";
        }
    }
}
