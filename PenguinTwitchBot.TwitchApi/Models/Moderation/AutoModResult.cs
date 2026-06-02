namespace PenguinTwitchBot.TwitchApi.Models.Moderation;

/// <summary>
/// Domain model for an AutoMod result entry.
/// </summary>
public record AutoModResult(
    string MsgId,
    bool IsPermitted);
