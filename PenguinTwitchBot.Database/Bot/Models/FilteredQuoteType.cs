namespace PenguinTwitchBot.Database.Bot.Models
{
    public class FilteredQuoteType
    {
        public Guid Id { get; set; }
        public DateTime CreatedOn { get; set; } = DateTime.MinValue;
        public string CreatedBy { get; set; } = "";
        public string Game { get; set; } = "";
        public string Quote { get; set; } = "";
    }
}
