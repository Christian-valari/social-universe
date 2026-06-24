using System.Collections.Generic;
using System.Threading.Tasks;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Economy;
using SocialUniverse.Progression;
using UnityEngine;

namespace SocialUniverse.Travel
{
    // Response shape for the "GetFuelState"/"SpendFuel"/"RefillFuel" Cloud Code
    // functions. Public so tests can construct it for a fake IBackendClient.
    public class FuelStateResult
    {
        public bool  Success  = true;
        public float Fuel     = -1f;
        public float MaxFuel  = -1f;
        public int   NewBalance = -1; // only set by RefillFuel

        // Unity's Cloud Code SDK deserializes with MissingMemberHandling.Error (no
        // public way to relax it), so any field the live deployed function returns
        // that isn't declared here throws and the whole call fails — not just an
        // unused value being dropped. The currently-deployed RefillFuel apparently
        // returns an extra "roll" field this repo's RefillFuel.js doesn't (deploy
        // drift between local ServerCode/ and what's actually live on UGS); this
        // absorbs it harmlessly until the live function is redeployed from source.
        public object Roll;
    }

    // Server-backed fuel: recharges over time (computed server-side from a
    // last-update timestamp), spent on travel, refillable instantly for coins.
    // PlayerState.Fuel is the client-side view cache this keeps in sync —
    // same relationship as Wallet is to IEconomyService.
    public class FuelSystem
    {
        private readonly IBackendClient _backend;
        private readonly PlayerState    _playerState;
        private readonly Wallet         _wallet;
        private readonly EconomyConfig  _config;

        public FuelSystem(IBackendClient backend, PlayerState playerState, Wallet wallet, EconomyConfig config)
        {
            _backend     = backend;
            _playerState = playerState;
            _wallet      = wallet;
            _config      = config;
        }

        public async Task RefreshAsync()
        {
            var result = await _backend.CallAsync<FuelStateResult>("GetFuelState");
            Debug.LogWarning($"#{GetType().Name}# Refresh Fuel! -> Fuel: {result.Fuel}");
            Apply(result);
        }

        public bool CanAfford(float amount) => _playerState.Fuel >= amount;

        public async Task<bool> TrySpendAsync(float amount)
        {
            if (amount <= 0f) return true;

            var result = await _backend.CallAsync<FuelStateResult>("SpendFuel",
                new Dictionary<string, object> { { "amount", amount } });
            Apply(result);
            Debug.LogWarning($"#{GetType().Name}# Spent Fuel! -> Amount: {amount} | result: {result.Success}");
            return result != null && result.Success;
        }

        public async Task<bool> RefillAsync()
        {
            var result = await _backend.CallAsync<FuelStateResult>("RefillFuel");
            if (result == null) return false;

            Apply(result);
            if (result.Success && result.NewBalance >= 0) _wallet.SetCoins(result.NewBalance);
            return result.Success;
        }

        private void Apply(FuelStateResult result)
        {
            if (result == null) return;
            if (result.MaxFuel >= 0f) _playerState.SetMaxFuel(result.MaxFuel);
            if (result.Fuel    >= 0f) _playerState.SetFuel(result.Fuel);
        }
    }
}
