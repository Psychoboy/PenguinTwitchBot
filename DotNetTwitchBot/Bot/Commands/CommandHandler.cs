using System.Collections.Concurrent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Commands
{
    public class CommandHandler
    {
        ConcurrentDictionary<string, Command> Commands = new ConcurrentDictionary<string, Command>();

        public Command? GetCommand(string commandName)
        {
            if (Commands.ContainsKey(commandName))
            {
                return Commands[commandName];
            }
            return null;
        }

        public void AddCommand(BaseCommandProperties commandProperties, IBaseCommandService commandService)
        {
            var defaultCommandProperties = commandProperties as DefaultCommand;
            if (defaultCommandProperties != null)
            {
                Commands[defaultCommandProperties.CustomCommandName] = new Command(commandProperties, commandService);
            }
            else
            {
                Commands[commandProperties.CommandName] = new Command(commandProperties, commandService);
            }
        }

        public void UpdateCommandName(string oldCommandName, string newCommandName)
        {
            var commandService = GetCommand(oldCommandName);
            if (commandService == null) return;
            Commands.Remove(oldCommandName, out var _);
            Commands[newCommandName] = commandService;
        }

        public void RemoveCommand(string commandName)
        {
            var commandService = GetCommand(commandName);
            if (commandService == null) return;
            Commands.Remove(commandName, out var _);
        }
    }
}