namespace PenguinTwitchBot.Helpers
{
    public static class FishingBoostDisplayHelper
    {
        // Formats a boost amount (e.g. 0.05 or -0.05) as a signed percentage string like "+5%" or "-5%".
        public static string FormatPercent(double boostAmount)
        {
            var rounded = (int)Math.Round(boostAmount * 100, MidpointRounding.AwayFromZero);
            return rounded >= 0 ? $"+{rounded}%" : $"{rounded}%";
        }
    }
}
