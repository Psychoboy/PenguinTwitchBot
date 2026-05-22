using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Models.Commands;
using PenguinTwitchBot.Bot.Models.Wheel;

namespace PenguinTwitchBot.Bot.Commands
{
    public interface IBaseCommandService
    {
        Task SendChatMessage(string message, bool sourceOnly = true);
        Task SendChatMessage(string name, string message, bool sourceOnly = true);
        Task OnCommand(object? sender, CommandEventArgs e);
        Task Register();
        Task<DefaultCommand> RegisterDefaultCommand(DefaultCommand defaultCommand);
        Task RespondWithMessage(CommandEventArgs e, string message, bool sourceOnly = true);
    }
}