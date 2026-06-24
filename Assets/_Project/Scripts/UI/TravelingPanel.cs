using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Net;
using SocialUniverse.Progression;
using SocialUniverse.Travel;

namespace SocialUniverse.UI
{
    // Lives in the dedicated Travel scene (loaded by TravelState while a trip
    // started from the Hub is in transit), so it's on screen for the entire
    // time this scene is loaded — no show/hide toggle needed against Sky
    // Discovery, that's a different scene entirely now. Land becomes
    // interactable once the server-issued arrival time has passed, and the
    // scene transition to PlanetState only happens once the server confirms
    // the trip can be landed.
    public class TravelingPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text   _targetNameText;
        [SerializeField] private TMP_Text   _arrivalTimeText;
        [SerializeField] private TMP_Text   _timeLeftText;
        [SerializeField] private Button     _landButton;

        [Inject] private PlayerState       _playerState;
        [Inject] private DatabaseRegistry  _registry;
        [Inject] private TravelTripSystem  _trips;
        [Inject] private HubState          _hub;
        [Inject] private ServerTime        _serverTime;

        private void Awake()
        {
            if (_landButton != null) _landButton.onClick.AddListener(OnLandClicked);
            if (_root != null) _root.SetActive(false);
        }

        private void OnEnable()
        {
            _playerState.OnTravelStateChanged += OnTravelStateChanged;
            Refresh(_playerState.IsTraveling, _playerState.TravelTargetId, _playerState.TravelArrivalTsMs);
        }

        private void OnDisable() => _playerState.OnTravelStateChanged -= OnTravelStateChanged;

        private void OnTravelStateChanged(bool traveling, string targetId, long arrivalTs) =>
            Refresh(traveling, targetId, arrivalTs);

        private void Refresh(bool traveling, string targetId, long arrivalTs)
        {
            if (_root != null) _root.SetActive(traveling);
            if (!traveling) return;

            var planet = _registry.GetPlanet(targetId);
            if (_targetNameText != null) _targetNameText.text = planet != null ? planet.DisplayName : targetId;
            if (_arrivalTimeText != null)
                _arrivalTimeText.text = $"Arrives at {System.DateTimeOffset.FromUnixTimeMilliseconds(arrivalTs).LocalDateTime.ToString("t")}";

            if (_landButton != null) _landButton.interactable = false;
        }

        private void Update()
        {
            if (!_playerState.IsTraveling) return;

            long nowMs  = new System.DateTimeOffset(_serverTime.UtcNow).ToUnixTimeMilliseconds();
            long leftMs = _playerState.TravelArrivalTsMs - nowMs;
            bool arrived = leftMs <= 0;

            if (_timeLeftText != null) _timeLeftText.text = TravelTimeFormat.FormatTimeLeft(leftMs);
            if (_landButton != null) _landButton.interactable = arrived;
        }

        private async void OnLandClicked()
        {
            if (_landButton != null) _landButton.interactable = false;

            var result = await _trips.LandAsync();
            if (result == null || !result.Success)
            {
                SULog.Warn($"TravelingPanel: land denied ({result?.Reason})", SULog.Channel.Travel);
                if (_landButton != null) _landButton.interactable = true;
                return;
            }

            EventBus.Publish(new TravelLandedEvent { PlanetId = result.TargetPlanetId });

            var planet = _registry.GetPlanet(result.TargetPlanetId);
            _hub.LandOnPlanet(planet);
        }
    }
}
