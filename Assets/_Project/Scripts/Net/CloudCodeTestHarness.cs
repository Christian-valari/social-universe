using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NaughtyAttributes;
using UnityEngine;
using VContainer;
using SocialUniverse.Core;

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

        private IBackendClient _backend;
        private IAuthService   _auth;

        [Inject]
        public void Construct(IBackendClient backend, IAuthService auth)
        {
            _backend = backend;
            _auth    = auth;
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
