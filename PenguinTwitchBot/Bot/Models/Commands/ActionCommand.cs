using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Bot.Models.Commands
{
    [Index(nameof(CommandName))]
    public class ActionCommand : BaseCommandProperties
    {
    }
}
