using System.Globalization;
using UnityEngine;

namespace SocialUniverse.UI
{
    // Which way a stat moves when switching from the active drone to a candidate drone.
    public enum DeltaDirection { Same, Up, Down }

    // One "old → new" stat row on an acquirable drone card. DroneGarageView owns the domain values
    // (active drone's effective stats vs the candidate's base stats); DroneRowView just presents this.
    public readonly struct DroneStatDeltaVm
    {
        public readonly string         Label;      // "Cargo"
        public readonly string         FromText;   // "50"  / "×1.0"
        public readonly string         ToText;     // "120" / "×1.6"
        public readonly DeltaDirection Direction;

        public DroneStatDeltaVm(string label, string fromText, string toText, DeltaDirection direction)
        {
            Label = label; FromText = fromText; ToText = toText; Direction = direction;
        }
    }

    // Pure formatting/direction math for the "why buy this drone" delta shown on acquirable cards.
    // No Unity object access — everything is primitives so it's trivially unit-testable.
    public static class DroneComparison
    {
        private const float Epsilon = 0.01f; // below this, treat two stat values as equal (float noise)

        public static DeltaDirection DirectionOf(float from, float to)
        {
            if (to - from > Epsilon) return DeltaDirection.Up;
            if (from - to > Epsilon) return DeltaDirection.Down;
            return DeltaDirection.Same;
        }

        // Integer-valued stat (Cargo, Speed): rounded whole numbers.
        public static DroneStatDeltaVm IntStat(string label, float from, float to) =>
            new DroneStatDeltaVm(label, Whole(from), Whole(to), DirectionOf(from, to));

        // Multiplier stat (Yield): "×1.6" with one decimal.
        public static DroneStatDeltaVm MultStat(string label, float from, float to) =>
            new DroneStatDeltaVm(label, Mult(from), Mult(to), DirectionOf(from, to));

        // The strongest hook: which asteroid tier this drone can crack.
        public static string TierLine(int tier) => $"Mines up to Tier {tier} asteroids";

        private static string Whole(float v) =>
            Mathf.RoundToInt(v).ToString(CultureInfo.InvariantCulture);

        private static string Mult(float v) =>
            "×" + v.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
