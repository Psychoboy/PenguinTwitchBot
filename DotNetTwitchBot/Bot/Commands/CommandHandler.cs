using DotNetTwitchBot.Bot.Commands.Moderation;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models.Commands;
using DotNetTwitchBot.Extensions;
using DotNetTwitchBot.Repository;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Commands
{
    public class CommandHandler(
        ILogger<CommandHandler> logger,
        IServiceScopeFactory scopeFactory,
        IKnownBots knownBots) : ICommandHandler
    {
        readonly ConcurrentDictionary<string, Command> Commands = new();

        public Command? GetCommand(string commandName)
        {
            if (Commands.TryGetValue(commandName, out var command))
            {
                return command;
            }
            return null;
        }

        public static bool CheckToRunBroadcasterOnly(CommandEventArgs eventArgs, BaseCommandProperties commandProperties)
        {
            if (commandProperties.RunFromBroadcasterOnly == false) return true;
            if (eventArgs.FromOwnChannel == false) return false;
            return true;
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
                logger.LogWarning("Command was missing id! This should not happen");
                return;
            }
            var originalCommand = await GetDefaultCommandById((int)defaultCommand.Id);
            if (originalCommand == null)
            {
                logger.LogWarning("Could not find the default command name {defaultCommandId}", defaultCommand.Id);
                return;
            }
            if (originalCommand.CustomCommandName.Equals(defaultCommand.CustomCommandName, StringComparison.OrdinalIgnoreCase) == false)
            {
                UpdateCommandName(originalCommand.CustomCommandName, defaultCommand.CustomCommandName);
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.DefaultCommands.Update(defaultCommand);
            await db.SaveChangesAsync();
            var command = GetCommand(defaultCommand.CustomCommandName);
            if (command == null)
            {
                logger.LogWarning("Could not get command: {CustomCommandName}", defaultCommand.CustomCommandName);
                return;
            }
            command.CommandProperties = defaultCommand;
        }

        public async Task<DefaultCommand?> GetDefaultCommandFromDb(string defaultCommandName)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.DefaultCommands.Find(x => x.CommandName.Equals(defaultCommandName)).FirstOrDefaultAsync();
        }

        public async Task<DefaultCommand?> GetDefaultCommandById(int id)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.DefaultCommands.Find(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<DefaultCommand>> GetDefaultCommandsFromDb()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return (await db.DefaultCommands.GetAllAsync()).ToList();
        }

        public async Task<DefaultCommand> AddDefaultCommand(DefaultCommand defaultCommand)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var newDefaultCommand = await db.DefaultCommands.AddAsync(defaultCommand);
            await db.SaveChangesAsync();
            await newDefaultCommand.ReloadAsync();
            return newDefaultCommand.Entity;
        }

        public async Task<IEnumerable<ExternalCommands>> GetExternalCommands()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.ExternalCommands.GetAllAsync();
        }

        public async Task<ExternalCommands?> GetExternalCommand(int id)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.ExternalCommands.GetByIdAsync(id);
        }

        public async Task AddOrUpdateExternalCommand(ExternalCommands externalCommand)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.ExternalCommands.Update(externalCommand);
            await db.SaveChangesAsync();
        }

        public async Task DeleteExternalCommand(ExternalCommands externalCommand)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.ExternalCommands.Remove(externalCommand);
            await db.SaveChangesAsync();
        }

        public async Task<bool> IsCoolDownExpired(string user, string command)
        {
            if(await GetGlobalCooldown(command) > DateTime.Now)
            {
                return false;
            }

            if (await GetUserCooldown(user, command) > DateTime.Now)
            {
                return false;
            }
            return true;
        }

        private async Task<DateTime> GetUserCooldown(string user, string command)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var userCooldown = await db.Cooldowns.Find(x => x.CommandName.Equals(command) && x.UserName.Equals(user)).FirstOrDefaultAsync();
            if (userCooldown == null)
            {
                return DateTime.MinValue;
            }
            return userCooldown.NextUserCooldownTime;
        }

        private async Task<DateTime> GetGlobalCooldown(string command)
        {
           await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var globalCooldown = await db.Cooldowns.Find(x => x.CommandName.Equals(command) && x.IsGlobal == true).FirstOrDefaultAsync();
            if (globalCooldown == null)
            {
                return DateTime.MinValue;
            }
            return globalCooldown.NextGlobalCooldownTime;
        }

        public async Task<bool> IsCoolDownExpiredWithMessage(string user, string displayName, string command)
        {
            if (!await IsCoolDownExpired(user, command))
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var serviceBackbone = scope.ServiceProvider.GetRequiredService<Core.IServiceBackbone>();
                await serviceBackbone.SendChatMessage(displayName, string.Format("!{0} is still on cooldown {1}", command, await CooldownLeft(user, command)));

                return false;
            }
            return true;
        }

        public async Task<bool> IsCoolDownExpiredWithMessage(string user, string displayName, BaseCommandProperties command)
        {
            if (!await IsCoolDownExpired(user, command.CommandName))
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var serviceBackbone = scope.ServiceProvider.GetRequiredService<Core.IServiceBackbone>();
                if (command is DefaultCommand commandProperties)
                {
                    await serviceBackbone.SendChatMessage(displayName, string.Format("!{0} is still on cooldown {1}", commandProperties.CustomCommandName, await CooldownLeft(user, command.CommandName)));
                }
                else
                {
                    await serviceBackbone.SendChatMessage(displayName, string.Format("!{0} is still on cooldown {1}", command, await CooldownLeft(user, command.CommandName)));
                }

                return false;
            }
            return true;
        }

        private async Task<string> CooldownLeft(string user, string command)
        {

            var globalCooldown = DateTime.MinValue;
            var userCooldown = DateTime.MinValue;
            var dbGlobalCooldown = await GetGlobalCooldown(command);
            if(dbGlobalCooldown > DateTime.Now)
            {
                globalCooldown = dbGlobalCooldown;
            }
            var dbUserCooldown = await GetUserCooldown(user, command);
            if (dbUserCooldown > DateTime.Now)
            {
                userCooldown = dbUserCooldown;
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

        public async Task AddCoolDown(string user, string command, int cooldown)
        {
            if (cooldown <= 0)
            {
                return;
            }
            await AddCoolDown(user, command, DateTime.Now.AddSeconds(cooldown));
        }

        public async Task AddCoolDown(string user, string command, DateTime cooldown)
        {
            if (cooldown <= DateTime.Now)
            {
                return;
            }
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var userCooldown = await db.Cooldowns.Find(x => x.CommandName.Equals(command) && x.UserName.Equals(user)).FirstOrDefaultAsync();
            if(userCooldown == null)
            {
                await db.Cooldowns.AddAsync(new CurrentCooldowns { CommandName = command, UserName = user, NextUserCooldownTime = cooldown });
            }
            else
            {
                userCooldown.NextUserCooldownTime = cooldown;
                db.Cooldowns.Update(userCooldown);
            }
            await db.SaveChangesAsync();
        }

        public async Task AddGlobalCooldown(string command, int cooldown)
        {
            if(cooldown <= 0)
            {
                return;
            }
            await AddGlobalCooldown(command, DateTime.Now.AddSeconds(cooldown));
        }

        public async Task AddGlobalCooldown(string command, DateTime cooldown)
        {
            if(cooldown <= DateTime.Now)
            {
                return;
            }
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var globalCooldown = await db.Cooldowns.Find(x => x.CommandName.Equals(command) && x.IsGlobal == true).FirstOrDefaultAsync();
            if (globalCooldown == null)
            {
                await db.Cooldowns.AddAsync(new CurrentCooldowns { CommandName = command, IsGlobal = true, NextGlobalCooldownTime = cooldown });
            }
            else
            {
                globalCooldown.NextGlobalCooldownTime = cooldown;
                db.Cooldowns.Update(globalCooldown);
            }
            await db.SaveChangesAsync();
        }

        public bool CommandExists(string command)
        {
            return Commands.ContainsKey(command);
        }

        public async Task<bool> CheckPermission(BaseCommandProperties commandProperties, CommandEventArgs eventArgs)
        {
            var passed = false;
            switch (commandProperties.MinimumRank)
            {
                case Rank.Viewer:
                case Rank.Regular:
                    passed = true;
                    break;
                case Rank.Follower:
                    using (var scope = scopeFactory.CreateAsyncScope())
                    {
                        var viewerService = scope.ServiceProvider.GetRequiredService<Commands.Features.IViewerFeature>();
                        passed = await viewerService.IsFollowerByUsername(eventArgs.Name);
                        break;
                    }
                case Rank.Subscriber:
                    passed = eventArgs.IsSubOrHigher();
                    break;
                case Rank.Vip:
                    passed = eventArgs.IsVipOrHigher();
                    break;
                case Rank.Moderator:
                    passed = eventArgs.IsModOrHigher();
                    break;
                case Rank.Streamer:
                    passed = eventArgs.IsBroadcaster || knownBots.IsStreamerOrBot(eventArgs.Name);
                    break;
                default:
                    passed = false;
                    break;
            }
            if(passed && string.IsNullOrEmpty(commandProperties.SpecificUserOnly) == false)
            {
                if (eventArgs.Name.Equals(commandProperties.SpecificUserOnly, StringComparison.OrdinalIgnoreCase) == false)
                {
                    passed = false;
                }
            }
            if (passed && commandProperties.SpecificUsersOnly.Count > 0)
            {
                if (commandProperties.SpecificUsersOnly.Contains(eventArgs.Name, StringComparer.CurrentCultureIgnoreCase) == false)
                {
                    passed = false;
                }
            }
            if (passed && commandProperties.SpecificRanks.Count > 0)
            {
                if (commandProperties.SpecificRanks.Contains(ConvertCommandEventArgsToRank(eventArgs)) == false)
                {
                    passed = false;
                }
            }
            return passed;
        }

        private Rank ConvertCommandEventArgsToRank(CommandEventArgs eventArgs)
        {
            if (eventArgs.IsBroadcaster || knownBots.IsStreamerOrBot(eventArgs.Name))
            {
                return Rank.Streamer;
            }
            if (eventArgs.IsMod)
            {
                return Rank.Moderator;
            }
            if (eventArgs.IsVip)
            {
                return Rank.Vip;
            }
            if (eventArgs.IsSub)
            {
                return Rank.Subscriber;
            }
            return Rank.Viewer;
        }

        public async Task ResetCooldown(CurrentCooldowns cooldown)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.Cooldowns.Remove(cooldown);
            await db.SaveChangesAsync();
        }

        public async Task<List<CurrentCooldowns>> GetCurrentCooldowns()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.Cooldowns.Find(x => x.NextUserCooldownTime > DateTime.Now || x.NextGlobalCooldownTime > DateTime.Now).ToListAsync();
        }
    }
}