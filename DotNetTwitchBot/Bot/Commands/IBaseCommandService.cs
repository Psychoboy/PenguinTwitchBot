using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands
{
    public interface IBaseCommandService
    {
        Task SendChatMessage(string message);
        Task SendChatMessage(string name, string message);
        Task OnCommand(object? sender, CommandEventArgs e);
        Task RegisterDefaultCommands();
        Task<DefaultCommand> RegisterDefaultCommand(DefaultCommand defaultCommand);
        bool IsCoolDownExpired(string user, string command);
        Task<bool> IsCoolDownExpiredWithMessage(string user, string displayName, string command);
        string CooldownLeft(string user, string command);
        void AddCoolDown(string user, string command, int cooldown);
        void AddCoolDown(string user, string command, DateTime cooldown);
        void AddGlobalCooldown(string command, int cooldown);
        void AddGlobalCooldown(string command, DateTime cooldown);
    }
}