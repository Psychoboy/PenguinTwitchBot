namespace PenguinTwitchBot.TwitchApi.Models.Chat;

/// <summary>
/// Domain request model for sending a chat message.
/// </summary>
public sealed class SendChatMessageRequest
{
    public required string BroadcasterId { get; init; }
    public required string SenderId { get; init; }
    public required string Message { get; init; }
    public bool? ForSourceOnly { get; init; }
    public string? ReplyParentMessageId { get; init; }
}
