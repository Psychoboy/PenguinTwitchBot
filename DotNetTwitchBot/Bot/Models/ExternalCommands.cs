namespace DotNetTwitchBot.Bot.Models
{
    public class ExternalCommands : BaseCommandProperties
    {
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
