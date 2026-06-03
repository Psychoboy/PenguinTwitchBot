using System.Diagnostics.CodeAnalysis;

namespace PenguinTwitchBot.TwitchApi.EventSub.Models.Chat;

public sealed class ChatPayItForward
{
    public string? RecipientUserId { get; set; }
    public string? RecipientUserName { get; set; }
    public string? RecipientUserLogin { get; set; }
    [MemberNotNullWhen(false, ["GifterUserId", "GifterUserLogin", "GifterUserName"])]
    public bool GifterIsAnonymous { get; set; }
    public string? GifterUserId { get; set; }
    public string? GifterUserName { get; set; }
    public string? GifterUserLogin { get; set; }
}