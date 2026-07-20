using UnityEngine;

namespace SocialUniverse.Travel
{
    // Client-side prediction of the server's time-based fuel recharge, anchored
    // at the last server sync. Mirrors the math in ServerCode/GetFuelState.js so
    // UI can tick smoothly between syncs without polling the backend.
    public static class FuelRechargeEstimator
    {
        // Fuel expected right now, given what the server reported `elapsedSeconds` ago.
        public static float PredictFuel(float syncedFuel, float maxFuel, float rechargePerHour, double elapsedSeconds)
        {
            if (maxFuel <= 0f) return 0f;
            if (rechargePerHour <= 0f || elapsedSeconds <= 0)
                return Mathf.Clamp(syncedFuel, 0f, maxFuel);

            float recharged = syncedFuel + (float)(elapsedSeconds / 3600.0) * rechargePerHour;
            return Mathf.Clamp(recharged, 0f, maxFuel);
        }

        // Seconds until the tank is full; 0 when already full, -1 when it never
        // recharges (non-positive rate).
        public static float SecondsToFull(float fuel, float maxFuel, float rechargePerHour)
        {
            if (fuel >= maxFuel) return 0f;
            if (rechargePerHour <= 0f) return -1f;
            return (maxFuel - fuel) / rechargePerHour * 3600f;
        }
    }
}
