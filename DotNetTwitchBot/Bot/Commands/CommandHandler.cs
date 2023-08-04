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
        private readonly ILogger<CommandHandler> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public CommandHandler(
            ILogger<CommandHandler> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

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

        public async Task<DefaultCommand?> GetDefaultCommandFromDb(string defaultCommandName)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                return await db.DefaultCommands.Where(x => x.CommandName.Equals(defaultCommandName)).FirstOrDefaultAsync();
            }
        }

        public async Task<DefaultCommand> AddDefaultCommand(DefaultCommand defaultCommand)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var newDefaultCommand = await db.DefaultCommands.AddAsync(defaultCommand);
                await db.SaveChangesAsync();
                await newDefaultCommand.ReloadAsync();
                return newDefaultCommand.Entity;
            }
        }
    }
}