
namespace DotNetTwitchBot.Bot.Commands
{
    public interface ICommandHelper
    {
        Task<bool> CommandExists(string command);
    }
}