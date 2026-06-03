using PenguinTwitchBot.TwitchApi.EventSub.Models.Charity;

namespace PenguinTwitchBot.TwitchApi.EventSub.Models.Chat;

public sealed class ChatCharityDonation
{
    public string Name { get; set; } = string.Empty;

    public CharityAmount Amount { get; set; } = new CharityAmount();
}
