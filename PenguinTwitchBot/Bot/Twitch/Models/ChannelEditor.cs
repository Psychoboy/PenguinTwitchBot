namespace PenguinTwitchBot.Bot.Twitch.Models;

/// <summary>
/// Domain model for a Twitch channel editor
/// </summary>
public record ChannelEditor(
    string UserId,
    string UserName,
    string UserLogin,
    DateTime CreatedAt);
