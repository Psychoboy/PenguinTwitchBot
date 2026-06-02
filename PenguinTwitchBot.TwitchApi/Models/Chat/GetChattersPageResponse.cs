namespace PenguinTwitchBot.TwitchApi.Models.Chat;

/// <summary>
/// Domain response model for a single page of chatters.
/// </summary>
public sealed record GetChattersPageResponse(
    IReadOnlyList<Chatter> Data,
    string? Cursor);
