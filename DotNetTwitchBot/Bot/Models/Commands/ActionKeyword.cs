using Microsoft.EntityFrameworkCore;

namespace DotNetTwitchBot.Bot.Models.Commands
{
    [Index(nameof(CommandName))]
    public class ActionKeyword : BaseCommandProperties
    {
        public string Response { get; set; } = "";
        public bool IsRegex { get; set; } = false;
        public bool IsCaseSensitive { get; set; } = false;
    }
}
