using PenguinTwitchBot.Database.Bot.Models.Fishing;

namespace PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations
{
    public class FishingAccidentTriggerConfiguration
    {
        /// <summary>
        /// Specific accident outcomes to trigger on (e.g. LineSnapped, RodSnapped, ReelJammed, TackleBoxLost, NetBroken). Empty = all accidents.
        /// </summary>
        public List<FishingAttemptOutcome> AccidentTypes { get; set; } = [];
    }
}
