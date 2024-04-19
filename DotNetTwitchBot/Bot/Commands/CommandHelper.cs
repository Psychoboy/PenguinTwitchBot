using DotNetTwitchBot.Bot.Commands.Alias;
using DotNetTwitchBot.Bot.Commands.AudioCommand;
using DotNetTwitchBot.Bot.Commands.Custom;

namespace DotNetTwitchBot.Bot.Commands
{
    public class CommandHelper(
        IAlias alias,
        AudioCommands audioCommands,
        CustomCommand customCommand,
        ICommandHandler commandHandler) : ICommandHelper
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

            if (customCommand.CustomCommandExists(command))
            {
                return true;
            }

            return false;
        }


    }
}
