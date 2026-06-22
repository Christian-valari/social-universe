using UnityEngine;
using UnityEngine.UI;
using VContainer;
using SocialUniverse.Config;
using SocialUniverse.Core;

namespace SocialUniverse.Travel
{
    // Returns the player to the Planet scene for whatever planet they're currently
    // staying on in the Hub. This isn't a real trip — TravelRangeMath blocks
    // targeting your own current planet, and there's no fuel cost for going back
    // to where you already are — so this calls HubState.TravelToPlanet directly
    // instead of going through the TravelRequestedEvent/TravelService pipeline.
    public class ReturnHomeButtonController : MonoBehaviour
    {
        [SerializeField] private Button _button;

        [Inject] private HubState         _hub;
        [Inject] private PlanetDefinition _currentPlanet;

        private void Awake() => _button.onClick.AddListener(OnClicked);

        private void OnClicked() => _hub.TravelToPlanet(_currentPlanet.PlanetId);
    }
}
