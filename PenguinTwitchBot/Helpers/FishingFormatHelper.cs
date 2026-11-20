namespace PenguinTwitchBot.Helpers
{
    public static class FishingFormatHelper
    {
        /// <summary>
        /// Formats large numbers cleanly for compact overlays (e.g., 500,000,000 -> "500M", 1,250,000,000 -> "1.25B").
        /// Values under 10,000 are shown with exact comma formatting.
        /// </summary>
        public static string FormatCompactNumber(long value)
        {
            var abs = Math.Abs((double)value);
            var sign = value < 0 ? "-" : "";

            if (abs < 10_000)
                return value.ToString("N0");
            if (abs < 1_000_000)
                return $"{sign}{TrimZeros(abs / 1_000.0)}K";
            if (abs < 1_000_000_000)
                return $"{sign}{TrimZeros(abs / 1_000_000.0)}M";
            if (abs < 1_000_000_000_000)
                return $"{sign}{TrimZeros(abs / 1_000_000_000.0)}B";
            return $"{sign}{TrimZeros(abs / 1_000_000_000_000.0)}T";
        }

        public static string FormatCompactNumber(double value)
        {
            if (!double.IsFinite(value)) return "0";
            var abs = Math.Abs(value);
            if (abs >= 10_000)
                return FormatCompactNumber((long)Math.Round(value));
            if (Math.Abs(value % 1) < 0.0001)
                return ((long)Math.Round(value)).ToString("N0");
            return value.ToString("N2");
        }

        public static string FormatScoreCategory(Database.Bot.Models.Fishing.FishingTournamentScoreCategory category) => category switch
        {
            Database.Bot.Models.Fishing.FishingTournamentScoreCategory.Largest => "Largest Weight",
            Database.Bot.Models.Fishing.FishingTournamentScoreCategory.Smallest => "Smallest Weight",
            Database.Bot.Models.Fishing.FishingTournamentScoreCategory.MostValuable => "Most Valuable",
            Database.Bot.Models.Fishing.FishingTournamentScoreCategory.MostCatches => "Most Catches",
            Database.Bot.Models.Fishing.FishingTournamentScoreCategory.TotalWeight => "Total Weight",
            Database.Bot.Models.Fishing.FishingTournamentScoreCategory.Average => "Best Average",
            Database.Bot.Models.Fishing.FishingTournamentScoreCategory.SpecificFish => "Specific Fish",
            _ => category.ToString()
        };

        private static string TrimZeros(double scaled)
        {
            if (scaled >= 100)
                return Math.Round(scaled, 1).ToString("0.#");
            return Math.Round(scaled, 2).ToString("0.##");
        }
    }
}
