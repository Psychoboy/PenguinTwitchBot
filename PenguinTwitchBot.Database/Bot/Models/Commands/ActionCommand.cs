using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Bot.Models.Commands
{
    [IndexAttribute(nameof(CommandName))]
    public class ActionCommand : BaseCommandProperties
    {
    }
}
