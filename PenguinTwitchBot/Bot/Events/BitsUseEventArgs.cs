namespace PenguinTwitchBot.Bot.Events
{
    public class BitsUseEventArgs
    {
        public string? UserId { get; set; }
        public string? Name { get; set; }
        public string? DisplayName { get; set; }
        public int Amount { get; set; }
        /// <summary>
        /// Plain text of the bits message, if any.
        /// </summary>
        public string? Message { get; set; }
        /// <summary>
        /// The use type: cheer, power_up, or custom_power_up.
        /// </summary>
        public string Type { get; set; } = string.Empty;
        public string? BroadcasterUserId { get; set; }
        public string? BroadcasterUserLogin { get; set; }
        public string? BroadcasterUserName { get; set; }
        /// <summary>
        /// True when the event is a Power-Up redemption.
        /// </summary>
        public bool IsPowerUp { get; set; }
        public PowerUp? PowerUp { get; set; }
        /// <summary>
        /// True when the event is a custom Power-Up redemption.
        /// </summary>
        public bool IsCustomPowerUp { get; set; }
        public CustomPowerUp? CustomPowerUp { get; set; }
        /// <summary>
        /// True when a chat message accompanied the bits use.
        /// </summary>
        public bool HasBitsMessage { get; set; }
        public BitsMessage? BitsMessage { get; set; }
    }

    /// <summary>
    /// Local Power-Up model — not the TwitchLib type.
    /// Possible types: message_effect, celebration, gigantify_an_emote
    /// </summary>
    public class PowerUp
    {
        public string Type { get; set; } = string.Empty;
        public string? EmoteId { get; set; }
        public string? EmoteName { get; set; }
    }

    /// <summary>
    /// Custom Power-Up model (channel-defined reward).
    /// </summary>
    public class CustomPowerUp
    {
        public string Title { get; set; } = string.Empty;
        public string RewardId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Local bits message model — not the TwitchLib type.
    /// </summary>
    public class BitsMessage
    {
        public string Text { get; set; } = string.Empty;
        public List<BitsEmote> Emotes { get; set; } = [];
    }

    /// <summary>
    /// Local bits emote/fragment model — not the TwitchLib type.
    /// Fragment type: text, cheermote, emote
    /// </summary>
    public class BitsEmote
    {
        public string Text { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? EmoteId { get; set; }
        public string? EmoteSetId { get; set; }
        public string? EmoteOwnerId { get; set; }
        public string[]? EmoteFormat { get; set; }
    }
}

