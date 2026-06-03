namespace PenguinTwitchBot.TwitchApi.EventSub.Models.Chat;
public sealed class ChatCheermote
{
    public string Prefix { get; set; } = string.Empty;
    public int Bits { get; set; }
    public int Tier { get; set; }
}
