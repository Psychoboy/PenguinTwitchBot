namespace PenguinTwitchBot.TwitchApi.EventSub.Models.ChannelSuspiciousUser
{
    public sealed class MessageFragment
    {
        /// <summary>
        /// The type of the message fragment. Valid values are:
        /// "text": A text fragment.
        /// "emote": An emote fragment.
        /// "cheermote": A cheermote fragment.
        /// </summary>
        public string Type { get; init; } = string.Empty;
        /// <summary>
        /// Message text in fragment.
        /// </summary>
        public string Text { get; init; } = string.Empty;
        public FragmentCheermote? Cheermote { get; init; }
        public FragmentEmote? Emote { get; init; }
    }
}