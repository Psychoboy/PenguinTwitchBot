namespace PenguinTwitchBot.Database.Bot.Models
{
    public class FilteredQuoteType
    {
        public Guid Id { get; set; }
        /// <summary>Viewer-facing quote number, assigned at runtime; see QuoteNumbering.</summary>
        public int Number { get; set; }
        public DateTime CreatedOn { get; set; } = DateTime.MinValue;
        public string CreatedBy { get; set; } = "";
        public string Game { get; set; } = "";
        public string Quote { get; set; } = "";
    }
}
