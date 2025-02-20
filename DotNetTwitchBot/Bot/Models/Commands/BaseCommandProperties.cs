using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Models.Commands
{
    [Index(nameof(CommandName))]
    public class BaseCommandProperties
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonIgnore]
        public int? Id { get; set; }
        public string CommandName { get; set; } = null!;
        public int UserCooldown { get; set; } = 0;
        public int GlobalCooldown { get; set; } = 0;
        public Rank MinimumRank { get; set; } = Rank.Viewer;
        public int Cost { get; set; } = 0;
        public bool Disabled { get; set; } = false;
        public bool SayCooldown { get; set; } = true;
        public bool SayRankRequirement { get; set; } = false;
        public bool ExcludeFromUi { get; set; } = false;
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
        public bool RunFromBroadcasterOnly { get; set; } = false;
        public string? SpecificUserOnly { get; set; } = null;
        public List<string> SpecificUsersOnly { get; set; } = [];
        public List<Rank> SpecificRanks { get; set; } = [];
    }
}