using System.Globalization;

namespace PenguinTwitchBot.Bot.Overlay
{
    /// <summary>
    /// Parses the "seconds or hh:mm:ss" duration format shared by the timer sub-actions and UI.
    /// </summary>
    public static class TimerDuration
    {
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

        /// <summary>Formats a value as hh:mm:ss for display.</summary>
        public static string Format(double seconds)
        {
            var clamped = double.IsFinite(seconds) && seconds > 0 ? seconds : 0;
            return TimeSpan.FromSeconds(clamped).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
        }

        private static bool IsUsable(double value, out double seconds)
        {
            seconds = 0;
            if (!double.IsFinite(value) || value < 0) return false;

            seconds = value;
            return true;
        }
    }
}
