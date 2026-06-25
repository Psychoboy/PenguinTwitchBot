using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Bot.Models.Commands
{
    [IndexAttribute(nameof(CommandName))]
    public class ActionCommand : BaseCommandProperties
    {
    }
}
