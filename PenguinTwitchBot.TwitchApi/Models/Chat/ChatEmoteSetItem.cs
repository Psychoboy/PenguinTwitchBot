namespace PenguinTwitchBot.TwitchApi.Models.Chat;

/// <summary>
/// Domain model for a single native Twitch emote (channel or global).
/// </summary>
public sealed record ChatEmoteSetItem(
    string Id,
    string Name,
    string ImageUrl1x);
