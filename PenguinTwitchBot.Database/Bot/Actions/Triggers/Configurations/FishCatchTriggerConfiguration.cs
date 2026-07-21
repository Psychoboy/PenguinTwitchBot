namespace PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations
{
    public class FishCatchTriggerConfiguration
    {
        /// <summary>
        /// Fish type IDs to match. Empty = any fish type.
        /// </summary>
        public List<int> FishTypeIds { get; set; } = [];

        /// <summary>
        /// Category names to match (fish must belong to at least one). Empty = any category.
        /// </summary>
        public List<string> Categories { get; set; } = [];

        /// <summary>
        /// Rarity names to match (e.g. "Common", "Rare"). Empty = any rarity.
        /// </summary>
        public List<string> Rarities { get; set; } = [];

        /// <summary>
        /// Minimum star rating required. 0 = any.
        /// </summary>
        public int MinStars { get; set; } = 0;

        /// <summary>
        /// Minimum weight in kg required. 0 = any.
        /// </summary>
        public double MinWeight { get; set; } = 0;

        /// <summary>
        /// Minimum gold earned required. 0 = any.
        /// </summary>
        public int MinGold { get; set; } = 0;

        /// <summary>
        /// When true, only fires if the caught fish is eligible for at least one active tournament.
        /// </summary>
        public bool RequireActiveTournament { get; set; } = false;
    }
}
