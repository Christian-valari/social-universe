using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Progression;

namespace SocialUniverse.Travel
{
    // Sole "look around the sky" view for the Hub (the StarMap list was removed).
    // Bodies are the real PlanetDefinition.ModelPrefab instances, placed at fixed
    // world bearings (SkyLockOnMath.GetSkyDirection) around the sky dome's center
    // (the camera stays put there — _currentPlanet itself is excluded from the sky
    // bodies, since you don't see the planet you're standing on floating in your
    // own sky). Distance along each bearing is a direct AU-to-unit conversion
    // of how far the body's PlanetDefinition.OrbitDistanceAU is from
    // _currentPlanet's (_auToWorldUnits), so spacing mirrors the real solar
    // system instead of being normalized to fit a fixed visual range. Model
    // scale is constant (_planetScaleMultiplier) — perspective alone, not a
    // scale fake, is what makes distant planets look smaller. _camera's
    // rotation is driven directly by GyroInputProvider.CurrentAttitude (gyro, or
    // mouse/touch-drag fallback) so the player looks around by rotating the
    // camera. Holding the look direction within _lockOnAngleThresholdDeg of a body
    // for _lockOnDwellSeconds locks onto it, surfacing the travel-cost/confirm
    // flow — any body in the sky can be confirmed, regardless of distance, so
    // Travel Time (TravelService.GetTravelDuration) is what actually matters.
    public class SkyDiscoveryController : MonoBehaviour
    {
        [SerializeField] private Transform _panel;
        [SerializeField] private GyroInputProvider _gyro;
        [SerializeField] private Camera   _camera;
        [SerializeField] private Transform _domeVcamTransform; // Cinemachine "Dome Vcam" transform — receives the manual rotation instead of _camera once a CinemachineBrain owns the Main Camera
        [SerializeField] private Transform _planetsRoot;
        [SerializeField] private TMP_Text _reticleText;
        [SerializeField] private TMP_Text _infoCostText;
        [SerializeField] private Button   _travelButton;
        [SerializeField] private float    _lockOnAngleThresholdDeg = 15f;
        [SerializeField] private float    _lockOnDwellSeconds      = 1.5f;
        [SerializeField] private float    _minOrbitRadius          = 8f;  // floor distance for the current planet's own near-zero AU offset (e.g. Earth/Moon)
        [SerializeField] private float    _auToWorldUnits          = 2f;  // world units per AU of OrbitDistanceAU difference from _currentPlanet
        [SerializeField] private float    _planetScaleMultiplier   = 14f;
        [SerializeField] private float    _highlightScaleMultiplier = 1.3f;

        [Inject] private DatabaseRegistry  _registry;
        [Inject] private TravelService     _travelService;
        [Inject] private PlayerState       _playerState;
        [Inject] private PlanetDefinition  _currentPlanet;

        private PlanetDefinition[] _planets;
        private Vector3[]          _directions;
        private Transform[]        _planetInstances;
        private Vector3[]          _baseScales;
        private Vector3            _originalCameraPosition;
        private int    _dwellIndex = -1;
        private float  _dwellTimer;
        private PlanetDefinition _locked;

        // True while a Cinemachine zoom (SkyZoomController) owns the camera —
        // suspends the manual rotation write and the dwell/lock-on raycast so
        // they don't fight the blend or re-lock onto a different planet.
        public bool IsCameraSuspended { get; set; }

        // World transform of the currently-locked planet's spawned model, or
        // null if nothing is locked. Frozen in place while IsCameraSuspended.
        public Transform LockedPlanetTransform =>
            _locked != null && _dwellIndex >= 0 && _planetInstances != null && _dwellIndex < _planetInstances.Length
                ? _planetInstances[_dwellIndex]
                : null;

        private void Awake()
        {
            _travelButton.onClick.AddListener(OnTravelClicked);
            if (_camera != null) _originalCameraPosition = _camera.transform.position;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<TravelPreviewClosedEvent>(OnPreviewClosed);
            var allPlanets = _registry.AllPlanets.OrderBy(p => p.OrbitOrder).ToArray();
            var allDirections = new Vector3[allPlanets.Length];
            for (int i = 0; i < allPlanets.Length; i++)
                allDirections[i] = SkyLockOnMath.GetSkyDirection(i, allPlanets.Length);

            int currentIndex = Array.IndexOf(allPlanets, _currentPlanet);

            // The sky dome center is the camera's fixed position — the player
            // looks out at the solar system from a single point. Each body's
            // distance along its bearing (computed in SpawnPlanets) is what
            // simulates being closer to or farther from any given planet.
            Vector3 domeCenter = _camera != null ? _originalCameraPosition : Vector3.zero;

            var skyPlanets    = new List<PlanetDefinition>();
            var skyDirections = new List<Vector3>();
            for (int i = 0; i < allPlanets.Length; i++)
            {
                if (i == currentIndex) continue;
                skyPlanets.Add(allPlanets[i]);
                skyDirections.Add(allDirections[i]);
            }
            _planets    = skyPlanets.ToArray();
            _directions = skyDirections.ToArray();

            if (_planetsRoot != null)
            {
                _planetsRoot.position = domeCenter;
                _planetsRoot.gameObject.SetActive(true);
            }
            SpawnPlanets();

            _dwellIndex = -1;
            _dwellTimer = 0f;
            _locked     = null;
            UpdateConfirmPanel();
        }

        private void OnDisable()
        {
            EventBus.Subscribe<TravelPreviewClosedEvent>(OnPreviewClosed);
            if (_planetsRoot != null) _planetsRoot.gameObject.SetActive(false);
            if (_camera != null)
            {
                _camera.transform.rotation = Quaternion.identity;
                _camera.transform.position = _originalCameraPosition;
            }
        }

        private void SpawnPlanets()
        {
            if (_planetsRoot == null) return;

            for (int i = _planetsRoot.childCount - 1; i >= 0; i--)
                Destroy(_planetsRoot.GetChild(i).gameObject);

            _planetInstances = new Transform[_planets.Length];
            _baseScales      = new Vector3[_planets.Length];
            for (int i = 0; i < _planets.Length; i++)
            {
                var prefab = _planets[i].ModelPrefab;
                if (prefab == null) continue;

                // Real AU distance from the current planet, directly converted to
                // world units — no per-session normalization, so spacing reflects
                // the actual solar system rather than always filling a fixed range.
                float distanceAU = Mathf.Abs(_planets[i].OrbitDistanceAU - _currentPlanet.OrbitDistanceAU);
                float radius     = _minOrbitRadius + distanceAU * _auToWorldUnits;

                var instance = Instantiate(prefab, _planetsRoot);
                instance.transform.localPosition  = _directions[i] * radius;
                instance.transform.localScale    *= _planetScaleMultiplier;

                _planetInstances[i] = instance.transform;
                _baseScales[i]      = instance.transform.localScale;
            }
        }

        private void Update()
        {
            if (_planets == null || _planets.Length == 0 || _gyro == null) return;
            if (IsCameraSuspended) return;

            var rotationTarget = _domeVcamTransform != null ? _domeVcamTransform : _camera != null ? _camera.transform : null;
            if (rotationTarget != null) rotationTarget.rotation = _gyro.CurrentAttitude;

            Vector3 lookDir = _gyro.CurrentAttitude * Vector3.forward;
            int closest = SkyLockOnMath.FindClosest(lookDir, _directions, out float angle);

            if (closest >= 0 && angle <= _lockOnAngleThresholdDeg)
            {
                if (closest != _dwellIndex)
                {
                    _dwellIndex = closest;
                    _dwellTimer = 0f;
                }
                _dwellTimer += Time.deltaTime;

                float progress01 = Mathf.Clamp01(_dwellTimer / _lockOnDwellSeconds);
                if (_reticleText != null)
                    _reticleText.text = $"Locking onto {_planets[closest].DisplayName}... {Mathf.RoundToInt(progress01 * 100f)}%";

                if (_dwellTimer >= _lockOnDwellSeconds && _locked != _planets[closest])
                {
                    _locked = _planets[closest];
                    UpdateConfirmPanel();
                }
            }
            else
            {
                _dwellIndex = -1;
                _dwellTimer = 0f;
                _locked     = null;
                if (_reticleText != null)
                    _reticleText.text = _gyro.IsGyroAvailable ? "Tilt the device to scan the sky..." : "Drag to scan the sky...";
                UpdateConfirmPanel();
            }

            RefreshHighlight();
        }

        private void RefreshHighlight()
        {
            if (_planetInstances == null) return;

            for (int i = 0; i < _planetInstances.Length; i++)
            {
                if (_planetInstances[i] == null) continue;
                _planetInstances[i].localScale = i == _dwellIndex
                    ? _baseScales[i] * _highlightScaleMultiplier
                    : _baseScales[i];
            }
        }

        private void UpdateConfirmPanel()
        {
            if (_locked == null)
            {
                if (_infoCostText != null) _infoCostText.text = "";
                _travelButton.interactable = false;
                return;
            }

            // int cost = _travelService.GetFuelCost(_locked);
            // if (_infoCostText != null)
            //     _infoCostText.text = cost <= 0 ? $"{_locked.DisplayName} — free trip home" : $"{_locked.DisplayName} — fuel cost: {cost}";
            //
            _travelButton.interactable = true;
        }

        private void OnTravelClicked()
        {
            if (_locked == null) return;
            _panel.gameObject.SetActive(false);
            EventBus.Publish(new TravelPreviewRequestedEvent { Planet = _locked });
        }
        
        private void OnPreviewClosed(TravelPreviewClosedEvent e)
        {
            _panel.gameObject.SetActive(true);
        }
    }
}
