namespace PenguinTwitchBot.TwitchApi.EventSub.Models.ChannelPoints;

public sealed class RedemptionReward
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Cost { get; set; }
    public string Prompt { get; set; } = string.Empty;  
}