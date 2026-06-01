namespace PenguinTwitchBot.Bot.Twitch.Models.Channels;

/// <summary>
/// Domain model for Twitch channel information
/// </summary>
public record ChannelInformation(
    string BroadcasterId,
    string BroadcasterLogin,
    string BroadcasterName,
    string BroadcasterLanguage,
    string GameId,
    string GameName,
    string Title,
    int Delay = 0);
