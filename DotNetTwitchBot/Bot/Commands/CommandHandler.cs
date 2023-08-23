using DotNetTwitchBot.Bot.Repository;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Commands
{
    public class CommandHandler : ICommandHandler
    {
        readonly ConcurrentDictionary<string, Command> Commands = new();
        readonly Dictionary<string, Dictionary<string, DateTime>> _coolDowns = new();
        readonly Dictionary<string, DateTime> _globalCooldowns = new();
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
            if (Commands.TryGetValue(commandName, out var command))
            {
                return command;
            }
            return null;
        }



        public string GetCommandDefaultName(string commandName)
        {
            if (Commands.TryGetValue(commandName, out var command))
            {
                return command.CommandProperties.CommandName;
            }
            return "";
        }

        public void AddCommand(BaseCommandProperties commandProperties, IBaseCommandService commandService)
        {
            if (commandProperties is DefaultCommand defaultCommandProperties)
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

        public async Task UpdateDefaultCommand(DefaultCommand defaultCommand)
        {
            if (defaultCommand.Id == null)
            {
                _logger.LogWarning("Command was missing id! This should not happen");
                return;
            }
            var originalCommand = await GetDefaultCommandById((int)defaultCommand.Id);
            if (originalCommand == null)
            {
                _logger.LogWarning($"Could not find the default command name {defaultCommand.Id}");
                return;
            }
            if (originalCommand.CustomCommandName.Equals(defaultCommand.CustomCommandName, StringComparison.CurrentCultureIgnoreCase) == false)
            {
                UpdateCommandName(originalCommand.CustomCommandName, defaultCommand.CustomCommandName);
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.DefaultCommands.Update(defaultCommand);
            await db.SaveChangesAsync();
            var command = GetCommand(defaultCommand.CustomCommandName);
            if (command == null)
            {
                _logger.LogWarning($"Could not get command: {defaultCommand.CustomCommandName}");
                return;
            }
            command.CommandProperties = defaultCommand;
        }

        public async Task<DefaultCommand?> GetDefaultCommandFromDb(string defaultCommandName)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.DefaultCommands.Find(x => x.CommandName.Equals(defaultCommandName)).FirstOrDefaultAsync();
        }

        public async Task<DefaultCommand?> GetDefaultCommandById(int id)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.DefaultCommands.Find(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<DefaultCommand>> GetDefaultCommandsFromDb()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return (await db.DefaultCommands.GetAllAsync()).ToList();
        }

        public async Task<DefaultCommand> AddDefaultCommand(DefaultCommand defaultCommand)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var newDefaultCommand = await db.DefaultCommands.AddAsync(defaultCommand);
            await db.SaveChangesAsync();
            await newDefaultCommand.ReloadAsync();
            return newDefaultCommand.Entity;
        }

        public async Task<IEnumerable<ExternalCommands>> GetExternalCommands()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.ExternalCommands.GetAllAsync();
        }

        public async Task<ExternalCommands?> GetExternalCommand(int id)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.ExternalCommands.GetByIdAsync(id);
        }

        public async Task AddOrUpdateExternalCommand(ExternalCommands externalCommand)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.ExternalCommands.Update(externalCommand);
            await db.SaveChangesAsync();
        }

        public async Task DeleteExternalCommand(ExternalCommands externalCommand)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.ExternalCommands.Remove(externalCommand);
            await db.SaveChangesAsync();
        }

        public bool IsCoolDownExpired(string user, string command)
        {
            if (
                _globalCooldowns.ContainsKey(command) &&
                _globalCooldowns[command] > DateTime.Now)
            {
                return false;
            }
            if (_coolDowns.ContainsKey(user.ToLower()))
            {
                if (_coolDowns[user.ToLower()].ContainsKey(command))
                {
                    if (_coolDowns[user.ToLower()][command] > DateTime.Now)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public async Task<bool> IsCoolDownExpiredWithMessage(string user, string displayName, string command)
        {
            if (!IsCoolDownExpired(user, command))
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var serviceBackbone = scope.ServiceProvider.GetRequiredService<Core.IServiceBackbone>();
                await serviceBackbone.SendChatMessage(displayName, string.Format("!{0} is still on cooldown {1}", command, CooldownLeft(user, command)));

                return false;
            }
            return true;
        }

        public async Task<bool> IsCoolDownExpiredWithMessage(string user, string displayName, BaseCommandProperties command)
        {
            if (!IsCoolDownExpired(user, command.CommandName))
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var serviceBackbone = scope.ServiceProvider.GetRequiredService<Core.IServiceBackbone>();
                if (command is DefaultCommand commandProperties)
                {
                    await serviceBackbone.SendChatMessage(displayName, string.Format("!{0} is still on cooldown {1}", commandProperties.CustomCommandName, CooldownLeft(user, command.CommandName)));
                }
                else
                {
                    await serviceBackbone.SendChatMessage(displayName, string.Format("!{0} is still on cooldown {1}", command, CooldownLeft(user, command.CommandName)));
                }

                return false;
            }
            return true;
        }

        private string CooldownLeft(string user, string command)
        {

            var globalCooldown = DateTime.MinValue;
            var userCooldown = DateTime.MinValue;
            if (
               _globalCooldowns.ContainsKey(command) &&
               _globalCooldowns[command] > DateTime.Now)
            {
                globalCooldown = _globalCooldowns[command];
            }
            if (_coolDowns.ContainsKey(user.ToLower()))
            {
                if (_coolDowns[user.ToLower()].ContainsKey(command))
                {
                    if (_coolDowns[user.ToLower()][command] > DateTime.Now)
                    {
                        userCooldown = _coolDowns[user.ToLower()][command];
                    }
                }
            }

            if (globalCooldown == DateTime.MinValue && userCooldown == DateTime.MinValue)
            {
                return "";
            }

            if (globalCooldown > userCooldown)
            {
                var timeDiff = globalCooldown - DateTime.Now;
                return ": " + timeDiff.ToFriendlyString();
            }
            else if (userCooldown > globalCooldown)
            {
                var timeDiff = userCooldown - DateTime.Now;
                return "for you: " + timeDiff.ToFriendlyString();
            }
            return "";
        }

        public void AddCoolDown(string user, string command, int cooldown)
        {
            AddCoolDown(user, command, DateTime.Now.AddSeconds(cooldown));
        }

        public void AddCoolDown(string user, string command, DateTime cooldown)
        {
            if (!_coolDowns.ContainsKey(user.ToLower()))
            {
                _coolDowns[user.ToLower()] = new Dictionary<string, DateTime>();
            }

            _coolDowns[user.ToLower()][command] = cooldown;
        }

        public void AddGlobalCooldown(string command, int cooldown)
        {
            AddGlobalCooldown(command, DateTime.Now.AddSeconds(cooldown));
        }

        public void AddGlobalCooldown(string command, DateTime cooldown)
        {
            _globalCooldowns[command] = cooldown;
        }
    }
}