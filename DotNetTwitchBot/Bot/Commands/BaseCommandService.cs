using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands
{
    public abstract class BaseCommandService : IBaseCommandService //: IHostedService
    {
        Dictionary<string, Dictionary<string, DateTime>> _coolDowns = new Dictionary<string, Dictionary<string, DateTime>>();
        Dictionary<string, DateTime> _globalCooldowns = new Dictionary<string, DateTime>();
        private IServiceScopeFactory _scopeFactory;
        protected CommandHandler _commandHandler { get; }

        public BaseCommandService(ServiceBackbone serviceBackbone, IServiceScopeFactory scopeFactory, CommandHandler commandHandler)
        {
            _serviceBackbone = serviceBackbone;
            serviceBackbone.CommandEvent += OnCommand;
            _scopeFactory = scopeFactory;
            _commandHandler = commandHandler;
        }

        protected ServiceBackbone _serviceBackbone { get; }

        public async Task SendChatMessage(string message)
        {
            await _serviceBackbone.SendChatMessage(message);
        }

        public async Task SendChatMessage(string name, string message)
        {
            await _serviceBackbone.SendChatMessage(name, message);
        }

        // public virtual Task StartAsync(CancellationToken cancellationToken) { return Task.CompletedTask; }
        // public virtual Task StopAsync(CancellationToken cancellationToken) { return Task.CompletedTask; }

        public abstract Task OnCommand(object? sender, CommandEventArgs e);
        public abstract void RegisterDefaultCommands();

        public async Task<DefaultCommand> RegisterDefaultCommand(DefaultCommand defaultCommand)
        {
            var registeredDefaultCommand = await GetDefaultCommandFromDb(defaultCommand.CommandName);
            if (registeredDefaultCommand != null)
            {
                return registeredDefaultCommand;
            }
            else
            {
                return await AddDefaultCommand(defaultCommand);
            }
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
                await _serviceBackbone.SendChatMessage(displayName, string.Format("!{0} is still on cooldown: {1}", command, CooldownLeft(user, command)));
                return false;
            }
            return true;
        }

        public string CooldownLeft(string user, string command)
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
                // return FormatTimeSpan(timeDiff);
                return timeDiff.ToFriendlyString();
            }
            else if (userCooldown > globalCooldown)
            {
                var timeDiff = userCooldown - DateTime.Now;
                // return FormatTimeSpan(timeDiff);
                return timeDiff.ToFriendlyString();
            }
            return "";
        }

        private string FormatTimeSpan(TimeSpan timeDiff)
        {
            return timeDiff.ToString(@"d\d\ hh\hmm\mss\s")
            .TrimStart(' ', 'd', 'h', 'm', 's', '0');
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

        protected async Task RegisterDefaultCommand(string command, IBaseCommandService baseCommandService, string moduleName, Rank minimumRank = Rank.Viewer)
        {
            var defaultCommand = new DefaultCommand
            {
                CommandName = command,
                CustomCommandName = command,
                ModuleName = moduleName,
                MinimumRank = minimumRank
            };

            defaultCommand = await RegisterDefaultCommand(defaultCommand);
            _commandHandler.AddCommand(defaultCommand, baseCommandService);
        }
    }
}
