namespace PenguinTwitchBot.Bot.Models.Chat
{
    /// <summary>
    /// A single fragment of a chat message for the overlay.
    /// Type values: "text", "emote", "cheermote", "mention"
    /// Provider values: "twitch", "7tv", "bttv", "ffz" (non-twitch are stubs for now)
    /// </summary>
    public class ChatOverlayFragment
    {
        /// <summary>Fragment type: text | emote | cheermote | mention</summary>
        public string Type { get; set; } = "text";

        /// <summary>Plain text content (always populated).</summary>
        public string Text { get; set; } = "";

        /// <summary>Emote ID (populated for emote/cheermote fragments).</summary>
        public string? EmoteId { get; set; }

        /// <summary>Emote provider: "twitch" | "7tv" | "bttv" | "ffz"</summary>
        public string? EmoteProvider { get; set; }

        /// <summary>Pre-computed image URL for the emote (1x).</summary>
        public string? EmoteUrl { get; set; }

        /// <summary>Cheer amount (populated for cheermote fragments).</summary>
        public int? CheerAmount { get; set; }

        /// <summary>Cheer color (populated for cheermote fragments).</summary>
        public string? CheerColor { get; set; }
    }
}
