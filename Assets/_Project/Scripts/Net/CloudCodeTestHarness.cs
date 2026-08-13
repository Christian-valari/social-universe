using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NaughtyAttributes;
using UnityEngine;
using VContainer;
using SocialUniverse.Core;
using SocialUniverse.Config;

namespace SocialUniverse.Net
{
    // Dev-only harness for invoking ServerCode/ Cloud Code functions through IBackendClient
    // and inspecting their responses in the console. Add this component to a GameObject
    // inside the Bootstrap scene (under RootLifetimeScope) and trigger calls via the
    // component's context menu (⋮ in the Inspector) while in Play Mode, signed in.
    public class CloudCodeTestHarness : MonoBehaviour
    {
        [Header("GrantCoins / SpendCoins / GrantOfflineIncome")]
        [SerializeField] private int _coinsAmount = 100;

        [Header("GrantStardust")]
        [SerializeField] private int _stardustAmount = 10;

        [Header("ValidateMining")]
        [SerializeField] private int _claimedCoins       = 30;
        [SerializeField] private int _sessionDurationSec = 30;
        [SerializeField] private int _coinsPerSec        = 1;

        [Header("PurchaseLand")]
        [SerializeField] private string _tileId   = "tile_0";
        [SerializeField] private string _planetId = "earth";
        [SerializeField] private int    _price    = 100;

        [Header("SpendFuel")]
        [SerializeField] private float _fuelSpendAmount = 20f;

        [Header("StartTravel")]
        [SerializeField] private string _travelOriginPlanetId = "earth";
        [SerializeField] private string _travelTargetPlanetId = "venus";

        [Header("Instant Planet Jump (dev)")]
        [Tooltip("Drag the destination PlanetDefinition here, then hit 'Go To Planet (Instant)' in Play Mode to load that planet with no travel trip, fuel cost, or loading ceremony.")]
        [SerializeField] private PlanetDefinition _instantPlanet;

        private IBackendClient   _backend;
        private IAuthService     _auth;
        private GameStateMachine _fsm;
        private PlanetState      _planetState;
        private SceneLoader      _sceneLoader;

        [Inject]
        public void Construct(IBackendClient backend, IAuthService auth,
            GameStateMachine fsm, PlanetState planetState, SceneLoader sceneLoader)
        {
            _backend     = backend;
            _auth        = auth;
            _fsm         = fsm;
            _planetState = planetState;
            _sceneLoader = sceneLoader;
        }

        [Button("Sign Out")]
        private void CallSignOut() => _ = SignOutAsync();

        [Button("Call GetServerTime")]
        private void CallGetServerTime() => _ = RunAsync<long>("GetServerTime");

        [Button("Call GetBootstrapState")]
        private void CallGetBootstrapState() => _ = RunAsync<BootstrapStateResult>("GetBootstrapState");

        [Button("Call GrantCoins")]
        private void CallGrantCoins() => _ = RunAsync<CurrencyGrantResult>("GrantCoins",
            new Dictionary<string, object> { ["amount"] = _coinsAmount });

        [Button("Call GrantStardust")]
        private void CallGrantStardust() => _ = RunAsync<CurrencyGrantResult>("GrantStardust",
            new Dictionary<string, object> { ["amount"] = _stardustAmount });

        [Button("Call SpendCoins")]
        private void CallSpendCoins() => _ = RunAsync<SpendResult>("SpendCoins",
            new Dictionary<string, object> { ["amount"] = _coinsAmount });

        [Button("Call ValidateMining")]
        private void CallValidateMining() => _ = RunAsync<MiningValidationResult>("ValidateMining",
            new Dictionary<string, object>
            {
                ["claimedCoins"]       = _claimedCoins,
                ["sessionDurationSec"] = _sessionDurationSec,
                ["coinsPerSec"]        = _coinsPerSec
            });

        [Button("Call GrantOfflineIncome")]
        private void CallGrantOfflineIncome() => _ = RunAsync<MiningValidationResult>("GrantOfflineIncome",
            new Dictionary<string, object> { ["claimedAmount"] = _coinsAmount });

        [Button("Call PurchaseLand")]
        private void CallPurchaseLand() => _ = RunAsync<PurchaseLandResult>("PurchaseLand",
            new Dictionary<string, object>
            {
                ["tileId"]   = _tileId,
                ["planetId"] = _planetId,
                ["price"]    = _price
            });

        [Button("Call GetFuelState")]
        private void CallGetFuelState() => _ = RunAsync<FuelStateResult>("GetFuelState");

        [Button("Call SpendFuel")]
        private void CallSpendFuel() => _ = RunAsync<FuelStateResult>("SpendFuel",
            new Dictionary<string, object> { ["amount"] = _fuelSpendAmount });

        [Button("Call RefillFuel")]
        private void CallRefillFuel() => _ = RunAsync<FuelStateResult>("RefillFuel");

        [Button("Call StartTravel")]
        private void CallStartTravel() => _ = RunAsync<TravelTripDebugResult>("StartTravel",
            new Dictionary<string, object>
            {
                ["originPlanetId"] = _travelOriginPlanetId,
                ["targetPlanetId"] = _travelTargetPlanetId
            });

        [Button("Call GetTravelState")]
        private void CallGetTravelState() => _ = RunAsync<TravelTripDebugResult>("GetTravelState");

        [Button("Call LandTravel")]
        private void CallLandTravel() => _ = RunAsync<TravelTripDebugResult>("LandTravel");

        [Button("Call GetCurrentPlanet")]
        private void CallGetCurrentPlanet() => _ = RunAsync<CurrentPlanetResult>("GetCurrentPlanet");

        [Button("Go To Planet (Instant)")]
        private void CallInstantPlanetJump() => _ = InstantPlanetJumpAsync();

        private async Task SignOutAsync()
        {
            if (!_auth.IsSignedIn)
            {
                SULog.Warn("CloudCodeTestHarness: cannot sign out — not signed in", SULog.Channel.Net);
                return;
            }

            var playerId = _auth.PlayerId;
            await _auth.SignOutAsync();
            SULog.Info($"CloudCodeTestHarness: signed out (was {playerId})", SULog.Channel.Net);
        }

        // Dev shortcut: jump straight into the Planet scene for _instantPlanet with no
        // travel trip / fuel cost / loading ceremony. Mirrors HubState.TravelToPlanet
        // (sets PlanetState.TargetPlanetId + persists LastPlanetId) but also handles the
        // already-on-a-planet case with an awaited same-scene reload, so the Planet scene
        // rebuilds against the new planet instead of racing its own load vs unload (both
        // PlanetState.Enter/Exit are fire-and-forget and SceneLoader is not queued).
        private async Task InstantPlanetJumpAsync()
        {
            if (_instantPlanet == null)
            {
                SULog.Warn("CloudCodeTestHarness: assign _instantPlanet before jumping", SULog.Channel.Net);
                return;
            }
            if (!_auth.IsSignedIn)
            {
                SULog.Warn("CloudCodeTestHarness: cannot jump — not signed in", SULog.Channel.Net);
                return;
            }

            _planetState.TargetPlanetId = _instantPlanet.PlanetId;
            PlayerPrefs.SetString(SaveKeys.LastPlanetIdKey(_auth.PlayerId), _instantPlanet.PlanetId);
            PlayerPrefs.Save();

            SULog.Info($"CloudCodeTestHarness: instant jump -> {_instantPlanet.PlanetId}", SULog.Channel.Net);

            if (_fsm.Current is PlanetState)
            {
                // Already on a planet: the FSM would re-enter the same PlanetState singleton
                // and race the Planet scene's unload against its reload. Do a clean awaited
                // reload instead (FSM stays in PlanetState).
                await _sceneLoader.UnloadAsync(Constants.SceneNames.Planet);
                await _sceneLoader.LoadAsync(Constants.SceneNames.Planet);
            }
            else
            {
                // From Hub / Auth / mini-game / etc.: let the FSM exit the current scene
                // and enter the Planet scene normally.
                _fsm.TransitionTo(_planetState);
            }
        }

        private async Task RunAsync<T>(string function, Dictionary<string, object> args = null)
        {
            if (!_auth.IsSignedIn)
            {
                SULog.Warn($"CloudCodeTestHarness: cannot call '{function}' — not signed in", SULog.Channel.Net);
                return;
            }

            SULog.Info($"CloudCodeTestHarness: calling '{function}' (player {_auth.PlayerId})...", SULog.Channel.Net);
            try
            {
                var result = await _backend.CallAsync<T>(function, args);
                SULog.Info($"CloudCodeTestHarness: '{function}' → {Describe(result)}", SULog.Channel.Net);
            }
            catch (Exception ex)
            {
                SULog.Error($"CloudCodeTestHarness: '{function}' failed — {ex.Message}", SULog.Channel.Net);
            }
        }

        private static string Describe<T>(T result)
        {
            if (result is BootstrapStateResult boot)
            {
                var balances = boot.balances != null
                    ? string.Join(", ", FormatBalances(boot.balances))
                    : "none";
                return $"serverTimeMs={boot.serverTimeMs}, balances=[{balances}], profile={boot.profile?.ToString() ?? "null"}";
            }
            return result?.ToString() ?? "null";
        }

        private static IEnumerable<string> FormatBalances(Dictionary<string, long> balances)
        {
            foreach (var kvp in balances)
                yield return $"{kvp.Key}={kvp.Value}";
        }

        [Serializable]
        private class CurrencyGrantResult
        {
            public long newBalance;
            public override string ToString() => $"newBalance={newBalance}";
        }

        [Serializable]
        private class SpendResult
        {
            public bool success;
            public long newBalance;
            public override string ToString() => $"success={success}, newBalance={newBalance}";
        }

        [Serializable]
        private class MiningValidationResult
        {
            public long granted;
            public long newBalance;
            public override string ToString() => $"granted={granted}, newBalance={newBalance}";
        }

        [Serializable]
        private class FuelStateResult
        {
            public bool  success = true;
            public string reason = "";
            public float fuel    = -1f;
            public float maxFuel = -1f;
            public int   newBalance = -1; // only set by RefillFuel

            public override string ToString() => $"success={success}, reason={reason}, fuel={fuel}, maxFuel={maxFuel}, newBalance={newBalance}";
        }

        [Serializable]
        private class TravelTripDebugResult
        {
            public bool   success;
            public string reason;
            public bool   traveling;
            public string targetPlanetId;
            public long   arrivalTs;
            public float  fuel    = -1f;
            public float  maxFuel = -1f;
            public override string ToString() =>
                $"success={success}, reason={reason}, traveling={traveling}, targetPlanetId={targetPlanetId}, " +
                $"arrivalTs={arrivalTs} ({(arrivalTs > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(arrivalTs).ToLocalTime().ToString("T") : "n/a")}), " +
                $"fuel={fuel}, maxFuel={maxFuel}";
        }

        [Serializable]
        private class PurchaseLandResult
        {
            public bool   success;
            public string tileId;
            public string ownerId;
            public long   newBalance;
            public string reason;
            public override string ToString() =>
                success
                    ? $"success=true, tileId={tileId}, ownerId={ownerId}, newBalance={newBalance}"
                    : $"success=false, reason={reason}";
        }

        [Serializable]
        private class BootstrapStateResult
        {
            public long serverTimeMs;
            public Dictionary<string, long> balances;
            public object profile;
        }
    }
}
