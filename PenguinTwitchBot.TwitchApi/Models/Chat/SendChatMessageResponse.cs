namespace PenguinTwitchBot.TwitchApi.Models.Chat;

/// <summary>
/// Domain response model for sending a chat message.
/// </summary>
public sealed record SendChatMessageResponse(
    IReadOnlyList<SendChatMessageResult> Data);

public sealed record SendChatMessageResult(
    string MessageId,
    bool IsSent,
    SendChatMessageDropReason? DropReason);

public sealed record SendChatMessageDropReason(
    string? Code,
    string? Message);
