namespace PenguinTwitchBot.Bot.Models.Chat
{
    /// <summary>
    /// A Twitch badge reference for the chat overlay.
    /// The browser resolves the image URL from the badge cache fetched via /api/chat/badges.
    /// </summary>
    public class ChatOverlayBadge
    {
        /// <summary>Badge set ID, e.g. "subscriber", "moderator", "bits".</summary>
        public string SetId { get; set; } = "";

        /// <summary>Badge version/tier, e.g. "12" for a 12-month sub badge.</summary>
        public string Id { get; set; } = "";
    }
}
