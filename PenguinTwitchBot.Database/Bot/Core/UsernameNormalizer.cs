namespace PenguinTwitchBot.Bot.Core
{
    public static class UsernameNormalizer
    {
        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Trim().ToLowerInvariant();
        }
    }
}
