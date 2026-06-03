namespace PenguinTwitchBot.TwitchApi.EventSub.Channel;

public sealed class ChannelChatMessagePayload : EventSubPayload<ChannelChatMessage>
{
}

public sealed class ChannelChatMessage
{
    public required string MessageId { get; set; }
    public required string ChatterUserId { get; set; }
    public required string ChatterUserLogin { get; set; }
    public required string ChatterUserName { get; set; }
    public bool IsSubscriber { get; set; }
    public bool IsModerator { get; set; }
    public bool IsVip { get; set; }
    public bool IsBroadcaster { get; set; }
    public required string Message { get; set; }
    public ChannelChatMessageFragment[] Fragments { get; set; } = [];
    public ChatBadge[] Badges { get; set; } = [];
    public string Color { get; set; } = string.Empty;
    public required string ChannelPointsCustomRewardId { get; set; }
    public string? SourceBroadcasterUserId { get; set; }
}

public sealed class ChannelChatMessageFragment
{
    public string Type { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public ChannelChatMessageFragmentEmote? Emote { get; set; }
    public ChannelChatMessageFragmentCheermote? Cheermote { get; set; }
    public ChannelChatMessageMention? Mention { get; set; }
}

public sealed class ChannelChatMessageMention
{
    public string UserId { get; set; } = string.Empty;
    public string UserLogin { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
}

public sealed class ChannelChatMessageFragmentEmote
{
    public string Id { get; set; } = string.Empty;
    public string? EmoteSetId { get; set; }
    public string? OwnerId { get; set; }
    public string[]? Format { get; set; }
}

public sealed class ChannelChatMessageFragmentCheermote
{
    public string Prefix { get; set; } = string.Empty;
    public int Bits { get; set; }
    public int Tier { get; set; }
}
