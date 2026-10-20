namespace PenguinTwitchBot.Bot.Commands.Games.Common
{
    /// <summary>Resolves the stable per-viewer key used by all minigames for per-user limits and round state.</summary>
    public static class DropGameUserKey
    {
        /// <summary>Prefers the Twitch user id; falls back to the chat name, then to "viewer".</summary>
        public static string Resolve(string? userId, string? name)
            => !string.IsNullOrWhiteSpace(userId) ? userId : (name ?? "viewer");
    }
}
