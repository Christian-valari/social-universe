using UnityEngine;
using SocialUniverse.Config;
using SocialUniverse.Core;

namespace SocialUniverse.Travel
{
    // Lives in the TravelLoading scene (3D rocket + planet), shown for both legs
    // of a trip: SolarSystem -> Travel (takeoff, shows the player's current
    // planet — you depart from where you are) and Travel -> Planet (landing,
    // shows the destination planet). TravelLoadingState (Core/FSM) loads this scene
    // and publishes TravelLoadingTakeOffRequestedEvent/TravelLoadingLandRequestedEvent
    // once it's ready — those (rather than Travel's own TravelConfirmedEvent/
    // TravelLandedEvent) are used here because this scene isn't loaded yet when
    // those fire; the Core-layer events exist so Core can signal this Travel-module
    // controller without Core depending on Travel.
    // The RocketCenter AnimatorController has no parameters/transitions wired
    // (takeOff/land/Idle are disconnected states) — so Animator.Play(stateName)
    // is used to hard-cut into a state rather than SetTrigger/SetBool.
    public class TravelLoadingController : MonoBehaviour
    {
        private const string TakeOffStateName = "takeOff";
        private const string LandStateName    = "land";

        [SerializeField] private Animator _rocketAnimator;
        [SerializeField] private Transform _planetHolder;
        [SerializeField] private GameObject _initialPlanetModel; // placeholder already in the scene; replaced on first AssignPlanet call

        private GameObject _currentPlanetModel;

        private void Awake()
        {
            _currentPlanetModel = _initialPlanetModel;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<TravelLoadingTakeOffRequestedEvent>(OnTakeOffRequested);
            EventBus.Subscribe<TravelLoadingLandRequestedEvent>(OnLandRequested);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TravelLoadingTakeOffRequestedEvent>(OnTakeOffRequested);
            EventBus.Unsubscribe<TravelLoadingLandRequestedEvent>(OnLandRequested);
        }

        private void OnTakeOffRequested(TravelLoadingTakeOffRequestedEvent e)
        {
            AssignPlanet(e.Planet);
            PlayTakeOff();
        }

        private void OnLandRequested(TravelLoadingLandRequestedEvent e)
        {
            AssignPlanet(e.Planet);
            PlayLand();
        }

        // Swaps the destination planet's model into PlanetHolder, replacing
        // whatever is currently shown (the scene's placeholder Earth, or a
        // previously assigned planet). PlanetHolder's own transform already
        // positions/scales the whole group in world space — only the model
        // child is replaced, at local zero so it sits exactly where the
        // placeholder did.
        public void AssignPlanet(PlanetDefinition planet)
        {
            if (planet == null || planet.ModelPrefab == null || _planetHolder == null) return;

            if (_currentPlanetModel != null) Destroy(_currentPlanetModel);

            _currentPlanetModel = Instantiate(planet.ModelPrefab, _planetHolder);
            _currentPlanetModel.transform.localPosition = Vector3.zero;
            _currentPlanetModel.transform.localRotation = Quaternion.identity;
        }

        public void PlayTakeOff()
        {
            if (_rocketAnimator != null) _rocketAnimator.Play(TakeOffStateName);
        }

        public void PlayLand()
        {
            if (_rocketAnimator != null) _rocketAnimator.Play(LandStateName);
        }
    }
}
