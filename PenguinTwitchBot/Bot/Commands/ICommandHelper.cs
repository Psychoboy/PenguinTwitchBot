namespace PenguinTwitchBot.Bot.Commands
{
    public interface ICommandHelper
    {
        Task<bool> CommandExists(string command);
    }
}