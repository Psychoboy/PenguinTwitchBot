namespace PenguinTwitchBot.TwitchApi.EventSub.Models.Bits
{
    public class BitsEmote
    {
        public string Id { get; set; } = string.Empty;
        public string EmoteSetId { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;
        public string[] Format { get; set; } = [];
    }
}