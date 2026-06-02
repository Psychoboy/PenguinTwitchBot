namespace PenguinTwitchBot.TwitchApi.EventSub.Channel;

public sealed class ChannelBitsUsePayload : EventSubPayload<ChannelBitsUse>
{
}

public sealed class ChannelBitsUse
{
    public required string UserId { get; set; }
    public required string UserLogin { get; set; }
    public required string UserName { get; set; }
    public int Bits { get; set; }
    public required string Type { get; set; }
    public required string BroadcasterUserId { get; set; }
    public required string BroadcasterUserLogin { get; set; }
    public required string BroadcasterUserName { get; set; }
    public BitsMessage? Message { get; set; }
    public BitsUserPowerUp? PowerUp { get; set; }
    public BitsCustomPowerUp? CustomPowerUp { get; set; }
}

public sealed class BitsMessage
{
    public required string Text { get; set; }
    public List<BitsChatFragment>? Fragments { get; set; }
}

public sealed class BitsChatFragment
{
    public required string Type { get; set; }
    public required string Text { get; set; }
    public BitsEmote? Emote { get; set; }
}

public sealed class BitsEmote
{
    public required string Id { get; set; }
    public string? EmoteSetId { get; set; }
    public string? OwnerId { get; set; }
    public string[]? Format { get; set; }
}

public sealed class BitsUserPowerUp
{
    public required string Type { get; set; }
    public BitsEmote? Emote { get; set; }
}

public sealed class BitsCustomPowerUp
{
    public required string Title { get; set; }
    public required string RewardId { get; set; }
}
