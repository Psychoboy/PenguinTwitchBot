using PenguinTwitchBot.Bot.Commands.Alias;
using PenguinTwitchBot.Bot.Commands.AudioCommand;
using PenguinTwitchBot.Bot.Features;

namespace PenguinTwitchBot.Bot.Commands
{
    public class CommandHelper(
        IAlias alias,
        AudioCommands audioCommands,
        ICommandHandler commandHandler,
        IFeatureRuntimeCoordinator featureRuntimeCoordinator) : ICommandHelper
    {
        public async Task<bool> CommandExists(string command)
        {
            if (featureRuntimeCoordinator.IsEnabled(FeatureKeys.Alias) && await alias.CommandExists(command))
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
