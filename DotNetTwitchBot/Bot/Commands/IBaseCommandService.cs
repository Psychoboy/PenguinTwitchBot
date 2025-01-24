using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models.Wheel;

namespace DotNetTwitchBot.Bot.Commands
{
    public interface IBaseCommandService
    {
        Task SendChatMessage(string message);
        Task SendChatMessage(string name, string message);
        Task OnCommand(object? sender, CommandEventArgs e);
        Task Register();
        Task<DefaultCommand> RegisterDefaultCommand(DefaultCommand defaultCommand);
    }
}