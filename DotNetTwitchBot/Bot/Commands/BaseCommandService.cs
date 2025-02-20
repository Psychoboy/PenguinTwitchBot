using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models.Commands;

namespace DotNetTwitchBot.Bot.Commands
{
    public abstract class BaseCommandService : IBaseCommandService //: IHostedService
    {
        protected ICommandHandler CommandHandler { get; }
        public string ModuleName { get; private set; }

        protected BaseCommandService(IServiceBackbone serviceBackbone, ICommandHandler commandHandler, string moduleName)
        {
            ServiceBackbone = serviceBackbone;
            serviceBackbone.CommandEvent += OnCommand;
            CommandHandler = commandHandler;
            ModuleName = moduleName;
        }

        protected IServiceBackbone ServiceBackbone { get; }

        public async Task SendChatMessage(string message)
        {
            await ServiceBackbone.SendChatMessage(message);
        }

        public async Task SendChatMessage(string name, string message)
        {
            await ServiceBackbone.SendChatMessage(name, message);
        }

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
