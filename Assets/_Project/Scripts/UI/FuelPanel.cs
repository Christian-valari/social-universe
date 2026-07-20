using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;
using SocialUniverse.Config;
using SocialUniverse.Economy;
using SocialUniverse.Progression;
using SocialUniverse.Safety;
using SocialUniverse.Travel;

namespace SocialUniverse.UI
{
    // Fuel info modal opened from the HUD's Fuel button: current/max fuel,
    // recharge rate, live countdown to full, and an instant coin refill.
    // Same show/hide modal shape as SettingsPanel. Fuel between server syncs
    // is predicted client-side (FuelRechargeEstimator) so the readout ticks
    // smoothly without polling the backend.
    public class FuelPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text _fuelText;
        [SerializeField] private Slider   _fuelSlider;
        [SerializeField] private TMP_Text _rechargeRateText;
        [SerializeField] private TMP_Text _rechargeTimeText;
        [SerializeField] private Button   _refillButton;
        [SerializeField] private TMP_Text _refillLabel;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private Button   _closeButton;

        [Inject] private FuelSystem    _fuelSystem;
        [Inject] private PlayerState   _playerState;
        [Inject] private Wallet        _wallet;
        [Inject] private EconomyConfig _config;
        [Inject] private IAudioManager _audio;

        // Prediction anchor: what the server last reported, and when (realtime).
        private float  _syncedFuel;
        private double _syncRealtime;
        private bool   _refilling;

        private void Awake()
        {
            _refillButton.onClick.AddListener(OnRefillClicked);
            _closeButton.onClick.AddListener(Close);
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            _playerState.OnFuelChanged    += OnFuelSynced;
            _playerState.OnMaxFuelChanged += OnMaxFuelSynced;
        }

        private void OnDisable()
        {
            _playerState.OnFuelChanged    -= OnFuelSynced;
            _playerState.OnMaxFuelChanged -= OnMaxFuelSynced;
        }

        public void Open()
        {
            Anchor(_playerState.Fuel);
            _refilling = false;
            _statusText.text = "";
            _refillLabel.text = $"Refill ({_config.FuelRefillCost} coins)";
            _rechargeRateText.text = $"+{_config.FuelRechargePerHour:0.#} fuel / hour";
            _audio.PlaySfx(SfxId.OpenPanel);
            gameObject.SetActive(true);
            Refresh();
            SyncFromServer();
        }

        public void Close()
        {
            _audio.PlaySfx(SfxId.Cancel);
            gameObject.SetActive(false);
        }

        // Countdown and predicted fuel drift each frame; wallet affordability has
        // no change event to hook, so poll here like HUDController does.
        private void Update() => Refresh();

        private void Anchor(float fuel)
        {
            _syncedFuel   = fuel;
            _syncRealtime = Time.realtimeSinceStartupAsDouble;
        }

        private void OnFuelSynced(float fuel)  => Anchor(fuel);
        private void OnMaxFuelSynced(float _)  => Refresh();

        private async void SyncFromServer()
        {
            try
            {
                await _fuelSystem.RefreshAsync();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"#{GetType().Name}# Fuel refresh failed: {e.Message}");
            }
        }

        private void Refresh()
        {
            float  max     = _playerState.MaxFuel;
            double elapsed = Time.realtimeSinceStartupAsDouble - _syncRealtime;
            float  fuel    = FuelRechargeEstimator.PredictFuel(_syncedFuel, max, _config.FuelRechargePerHour, elapsed);

            _fuelText.text = $"{Mathf.FloorToInt(fuel)} / {Mathf.RoundToInt(max)}";
            _fuelSlider.maxValue = max;
            _fuelSlider.value    = fuel;

            float secondsToFull = FuelRechargeEstimator.SecondsToFull(fuel, max, _config.FuelRechargePerHour);
            _rechargeTimeText.text = secondsToFull switch
            {
                0f    => "Tank full",
                < 0f  => "Does not recharge",
                _     => $"Full in {TravelTimeFormat.FormatDuration(secondsToFull)}"
            };

            bool full = secondsToFull == 0f;
            _refillButton.interactable = !_refilling && !full && _wallet.CanAfford(_config.FuelRefillCost);
        }

        private async void OnRefillClicked()
        {
            if (_refilling) return;
            _audio.PlaySfx(SfxId.Confirm);
            _refilling = true;
            _statusText.text = "Refilling…";
            Refresh();

            bool success = false;
            try
            {
                success = await _fuelSystem.RefillAsync();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"#{GetType().Name}# Refill failed: {e.Message}");
            }

            if (this == null) return; // scene unloaded mid-request
            _refilling = false;
            _statusText.text = success ? "Tank refilled!" : "Refill failed";
            Refresh();
        }
    }
}
