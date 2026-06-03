namespace PenguinTwitchBot.TwitchApi.EventSub.Models.Charity
{
    public sealed class CharityAmount
    {
        public int Value { get; set; }
        public int DecimalPlaces { get; set; }
        public string Currency { get; set; } = string.Empty;
    }
}