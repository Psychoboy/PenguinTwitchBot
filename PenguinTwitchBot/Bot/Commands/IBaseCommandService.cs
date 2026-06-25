using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Database.Bot.Models.Commands;

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