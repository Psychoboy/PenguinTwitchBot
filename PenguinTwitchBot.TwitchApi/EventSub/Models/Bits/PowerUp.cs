namespace PenguinTwitchBot.TwitchApi.EventSub.Models.Bits
{
    public class PowerUp
    {
        public string Type { get; set; } = string.Empty;
        public string? MessageEffectId { get; set; }
        public PowerUpEmote? Emote { get; set; }
    }
}