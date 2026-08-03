using System;

namespace SocialUniverse.Economy
{
    // Pure slot-array helpers shared by the client build flow and mirrored by the
    // ServerCode PlaceBuild/RemoveBuild/MoveBuild functions. buildLevel is always
    // FilledCount(slots) — never an independently tracked counter.
    public static class LandBuildMath
    {
        public static string[] EnsureSize(string[] slots, int size)
        {
            if (slots != null && slots.Length == size) return slots;
            var result = new string[size];
            if (slots != null)
                Array.Copy(slots, result, Math.Min(slots.Length, size));
            return result;
        }

        public static int FilledCount(string[] slots)
        {
            if (slots == null) return 0;
            int n = 0;
            foreach (var s in slots)
                if (!string.IsNullOrEmpty(s)) n++;
            return n;
        }

        public static bool IsEmpty(string[] slots, int index) =>
            slots == null || index < 0 || index >= slots.Length || string.IsNullOrEmpty(slots[index]);
    }
}
