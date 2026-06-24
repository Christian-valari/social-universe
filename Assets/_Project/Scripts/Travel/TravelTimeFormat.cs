using System;

namespace SocialUniverse.Travel
{
    // Pure formatting for ETA/time-left strings shown in the preview and
    // traveling panels — kept separate from any MonoBehaviour so it's
    // unit-testable.
    public static class TravelTimeFormat
    {
        public static string FormatDuration(float seconds)
        {
            if (seconds <= 0f) return "Instant";
            return FormatTimeSpan(TimeSpan.FromSeconds(seconds));
        }

        public static string FormatTimeLeft(long millisecondsLeft)
        {
            if (millisecondsLeft <= 0) return "Arrived";
            return FormatTimeSpan(TimeSpan.FromMilliseconds(millisecondsLeft));
        }

        private static string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            if (ts.TotalMinutes >= 1) return $"{ts.Minutes}m {ts.Seconds}s";
            return $"{ts.Seconds}s";
        }
    }
}
