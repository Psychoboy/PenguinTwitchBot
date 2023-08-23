namespace DotNetTwitchBot.Bot.Models
{
    public class BaseCommandProperties
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int? Id { get; set; }
        public string CommandName { get; set; } = null!;
        public int UserCooldown { get; set; } = 0;
        public int GlobalCooldown { get; set; } = 0;
        public Rank MinimumRank { get; set; } = Rank.Viewer;
        public int Cost { get; set; } = 0;
        public bool Disabled { get; set; } = false;
        public bool SayCooldown { get; set; } = true;
        public bool SayRankRequirement { get; set; } = false;
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";

    }
}