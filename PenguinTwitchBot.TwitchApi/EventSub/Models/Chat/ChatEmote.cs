namespace PenguinTwitchBot.TwitchApi.EventSub.Models.Chat;

public sealed class ChatEmote
{
    public string Id { get; set; } = string.Empty;
    public string EmoteSetId { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string[] Format { get; set; } = [];
}