namespace PenguinTwitchBot.Bot.Services.Chat
{
    /// <summary>
    /// Assigns and persists per-user display colors for the chat overlay.
    /// If a user has a Twitch color set, that is returned. Otherwise a stable
    /// color from the palette is assigned and remembered for the session.
    /// </summary>
    public interface IChatColorService
    {
        /// <summary>
        /// Returns the display color for the given user.
        /// Uses <paramref name="twitchColor"/> if non-empty; otherwise assigns one
        /// from the palette and remembers it for subsequent calls.
        /// </summary>
        string GetOrAssignColor(string userId, string? twitchColor);
    }
}
