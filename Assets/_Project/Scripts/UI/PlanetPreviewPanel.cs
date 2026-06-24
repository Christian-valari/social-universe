using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Progression;
using SocialUniverse.Travel;

namespace SocialUniverse.UI
{
    // Shown when the Sky Discovery Launch button locks onto a planet. Displays
    // the model, fuel cost, and ETA; its own Launch button is the only thing
    // that actually commits to the trip (publishes TravelConfirmedEvent) — the
    // original Launch button now only opens this preview.
    public class PlanetPreviewPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Transform  _modelAnchor;
        [SerializeField] private TMP_Text   _nameText;
        [SerializeField] private TMP_Text   _fuelCostText;
        [SerializeField] private TMP_Text   _etaText;
        [SerializeField] private Button     _launchButton;
        [SerializeField] private Button     _cancelButton;

        [Inject] private TravelService _travelService;
        [Inject] private PlayerState   _playerState;

        private PlanetDefinition _pending;
        private GameObject       _modelInstance;

        private void Awake()
        {
            if (_launchButton != null) _launchButton.onClick.AddListener(OnLaunchClicked);
            if (_cancelButton != null) _cancelButton.onClick.AddListener(Close);
            if (_root != null) _root.SetActive(false);
        }

        private void OnEnable()  => EventBus.Subscribe<TravelPreviewRequestedEvent>(OnPreviewRequested);
        private void OnDisable() => EventBus.Unsubscribe<TravelPreviewRequestedEvent>(OnPreviewRequested);

        private void OnPreviewRequested(TravelPreviewRequestedEvent e) => Open(e.Planet);

        public void Open(PlanetDefinition planet)
        {
            _pending = planet;
            if (_nameText != null) _nameText.text = planet.DisplayName;

            int cost = _travelService.GetFuelCost(planet);
            if (_fuelCostText != null)
                _fuelCostText.text = cost <= 0 ? "Free" : $"{cost} fuel";

            float etaSec = _travelService.GetTravelDuration(planet);
            if (_etaText != null) _etaText.text = TravelTimeFormat.FormatDuration(etaSec);

            if (_launchButton != null)
                _launchButton.interactable = cost <= 0 || _playerState.Fuel >= cost;

            SpawnModel(planet);
            if (_root != null) _root.SetActive(true);
        }

        public void Close()
        {
            _pending = null;
            if (_modelInstance != null) Destroy(_modelInstance);
            if (_root != null) _root.SetActive(false);
            EventBus.Publish(new TravelPreviewClosedEvent());
        }

        private void SpawnModel(PlanetDefinition planet)
        {
            if (_modelInstance != null) Destroy(_modelInstance);
            if (_modelAnchor != null && planet.ModelPrefab != null)
                _modelInstance = Instantiate(planet.ModelPrefab, _modelAnchor);
        }

        private void OnLaunchClicked()
        {
            if (_pending == null) return;
            EventBus.Publish(new TravelConfirmedEvent { Planet = _pending });
            // Close();
        }
    }
}
