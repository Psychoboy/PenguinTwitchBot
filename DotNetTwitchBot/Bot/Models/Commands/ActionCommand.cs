using Microsoft.EntityFrameworkCore;

namespace DotNetTwitchBot.Bot.Models.Commands
{
    [Index(nameof(CommandName))]
    public class ActionCommand : BaseCommandProperties
    {
    }
}
