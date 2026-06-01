namespace PenguinTwitchBot.Bot.Twitch.Models.Channels;

/// <summary>
/// Domain model for a Twitch channel editor
/// </summary>
public record ChannelEditor(
    string UserId,
    string UserName,
    string UserLogin,
    DateTime CreatedAt);
