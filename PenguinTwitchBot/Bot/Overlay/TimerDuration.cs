using System.Globalization;

namespace PenguinTwitchBot.Bot.Overlay
{
    /// <summary>
    /// Parses the "seconds or hh:mm:ss" duration format shared by the timer sub-actions and UI.
    /// </summary>
    public static class TimerDuration
    {
        /// <summary>Well inside TimeSpan's range, so conversions can never overflow after rounding.</summary>
        private const double MaxSeconds = 100d * 24 * 60 * 60;

        public static bool TryParse(string? value, out double seconds)
        {
            seconds = 0;
            if (string.IsNullOrWhiteSpace(value)) return false;

            value = value.Trim();

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                return IsUsable(parsed, out seconds);

            if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var timeSpan))
                return IsUsable(timeSpan.TotalSeconds, out seconds);

            return false;
        }

        /// <summary>Formats a value as hh:mm:ss, using total hours so durations past a day stay accurate.</summary>
        public static string Format(double seconds)
        {
            var clamped = double.IsFinite(seconds) && seconds > 0 ? Math.Min(seconds, MaxSeconds) : 0;
            var span = TimeSpan.FromSeconds(clamped);

            return string.Create(CultureInfo.InvariantCulture,
                $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}");
        }

        private static bool IsUsable(double value, out double seconds)
        {
            seconds = 0;
            if (!double.IsFinite(value) || value < 0 || value > MaxSeconds) return false;

            seconds = value;
            return true;
        }
    }
}
