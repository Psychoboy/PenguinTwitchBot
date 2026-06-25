using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Bot.Models.Commands
{
    [IndexAttribute(nameof(CommandName))]
    public class ActionKeyword : BaseCommandProperties
    {
        public string Response { get; set; } = "";
        public bool IsRegex { get; set; } = false;
        public bool IsCaseSensitive { get; set; } = false;
    }
}
