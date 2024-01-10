using DotNetTwitchBot.Bot.Commands.Custom;

namespace DotNetTwitchBot.Bot.Commands
{
    public class CommandHelper(Alias alias, AudioCommands audioCommands, ICommandHandler commandHandler) : ICommandHelper
    {
        public async Task<bool> CommandExists(string command)
        {
            if (await alias.CommandExists(command))
            {
                return true;
            }

            if (await audioCommands.CommandExists(command))
            {
                return true;
            }

            if (commandHandler.CommandExists(command))
            {
                return true;
            }

            return false;
        }
    }
}
