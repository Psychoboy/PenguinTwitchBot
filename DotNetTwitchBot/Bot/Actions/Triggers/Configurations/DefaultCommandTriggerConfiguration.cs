namespace DotNetTwitchBot.Bot.Actions.Triggers.Configurations
{
    public class DefaultCommandTriggerConfiguration
    {
        public string DefaultCommandName { get; set; } = null!; // e.g., "gamble", "defuse"
        public string EventType { get; set; } = null!; // e.g., "Gamble.JackpotWin", "Defuse.Success"
    }

    public static class DefaultCommandEventTypes
    {
        // Gamble events
        public const string GambleJackpotWin = "Gamble.JackpotWin";
        public const string GambleWin = "Gamble.Win";
        public const string GambleLose = "Gamble.Lose";
        
        // Defuse events
        public const string DefuseSuccess = "Defuse.Success";
        public const string DefuseFailure = "Defuse.Failure";
    }
}
