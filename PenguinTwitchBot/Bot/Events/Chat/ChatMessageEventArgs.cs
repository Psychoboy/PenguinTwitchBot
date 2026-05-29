using PenguinTwitchBot.Bot.Models.Chat;

namespace PenguinTwitchBot.Bot.Events.Chat
{
    public class ChatMessageEventArgs : BaseChatEventArgs
    {
        public string Message { get; set; } = "";
        public bool FromOwnChannel { get; internal set; }

        /// <summary>
        /// Ordered message fragments (text, emote, cheermote, mention).
        /// Populated for overlay consumers; existing plain-text handlers can ignore this.
        /// </summary>
        public List<ChatOverlayFragment> Fragments { get; set; } = [];

        /// <summary>Badge references carried on the message event.</summary>
        public List<ChatOverlayBadge> Badges { get; set; } = [];

        /// <summary>
        /// Resolved display color — the user's Twitch color if set, otherwise a
        /// stable generated color assigned by ChatColorService.
        /// </summary>
        public string ResolvedColor { get; set; } = "#FFFFFF";
    }
}
