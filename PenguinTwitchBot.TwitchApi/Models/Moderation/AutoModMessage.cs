namespace PenguinTwitchBot.TwitchApi.Models.Moderation;

/// <summary>
/// Domain model for an AutoMod message check request item.
/// </summary>
public record AutoModMessage(
    string MsgId,
    string MsgText);
