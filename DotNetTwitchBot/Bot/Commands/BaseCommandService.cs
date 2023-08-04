using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands
{
    public abstract class BaseCommandService : IBaseCommandService //: IHostedService
    {
        protected CommandHandler CommandHandler { get; }

        public BaseCommandService(ServiceBackbone serviceBackbone, IServiceScopeFactory scopeFactory, CommandHandler commandHandler)
        {
            _serviceBackbone = serviceBackbone;
            serviceBackbone.CommandEvent += OnCommand;
            CommandHandler = commandHandler;
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
        public abstract Task Register();

        public async Task<DefaultCommand> RegisterDefaultCommand(DefaultCommand defaultCommand)
        {
            var registeredDefaultCommand = await CommandHandler.GetDefaultCommandFromDb(defaultCommand.CommandName);
            if (registeredDefaultCommand != null)
            {
                return registeredDefaultCommand;
            }
            else
            {
                return await CommandHandler.AddDefaultCommand(defaultCommand);
            }
        }

        protected async Task RegisterDefaultCommand(
            string command,
            IBaseCommandService baseCommandService,
            string moduleName,
            Rank minimumRank = Rank.Viewer,
            bool sayCooldown = true,
            bool sayRankRequirement = false,
            int userCooldown = 0,
            int globalCoolDown = 0)
        {
            var defaultCommand = new DefaultCommand
            {
                CommandName = command,
                CustomCommandName = command,
                ModuleName = moduleName,
                MinimumRank = minimumRank,
                SayCooldown = sayCooldown,
                SayRankRequirement = sayRankRequirement,
                UserCooldown = userCooldown,
                GlobalCooldown = globalCoolDown
            };

            defaultCommand = await RegisterDefaultCommand(defaultCommand);
            CommandHandler.AddCommand(defaultCommand, baseCommandService);
        }
    }
}
