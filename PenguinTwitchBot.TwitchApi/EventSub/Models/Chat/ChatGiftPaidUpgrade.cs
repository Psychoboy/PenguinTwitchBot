using System.Diagnostics.CodeAnalysis;

namespace PenguinTwitchBot.TwitchApi.EventSub.Models.Chat;

public sealed class ChatGiftPaidUpgrade
{
    [MemberNotNullWhen(false, ["GifterUserId", "GifterUserLogin", "GifterUserName"])]
    public bool GifterIsAnonymous { get; set; }
    public string? GifterUserId { get; set; }
    public string? GifterUserName { get; set; }
    public string? GifterUserLogin { get; set; }
}