using DotNetTwitchBot.Application.ChatMessage.Notification;
using DotNetTwitchBot.Application.ChatMessage.Notifications;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models.Commands;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands
{
    public abstract class BaseCommandService : IBaseCommandService //: IHostedService
    {
        protected ICommandHandler CommandHandler { get; }
        public string ModuleName { get; private set; }

        protected IMediator mediator;

        protected BaseCommandService(IServiceBackbone serviceBackbone, ICommandHandler commandHandler, string moduleName, IMediator mediator)
        {
            ServiceBackbone = serviceBackbone;
            serviceBackbone.CommandEvent += OnCommand;
            CommandHandler = commandHandler;
            ModuleName = moduleName;
            this.mediator = mediator;
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

        public async Task RespondWithMessage(CommandEventArgs e, string message)
        {
            message = message.TrimStart('!').Trim();

            switch(e.Platform)
            {
                case PlatformType.Twitch:
                    if (string.IsNullOrWhiteSpace(e.MessageId))
                    {
                        await ServiceBackbone.SendChatMessage(e.DisplayName, message);
                    }
                    else
                    {
                        await mediator.Publish(new ReplyToMessage(e.DisplayName, e.MessageId, message));
                    }
                    break;
                case PlatformType.Kick:
                    await ServiceBackbone.SendChatMessage(e.DisplayName, message, e.Platform);
                    break;
                default:
                    throw new NotImplementedException($"RespondWithMessage not implemented for platform type {e.Platform}");
            }
            
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
            int globalCoolDown = 0,
            string description = "")
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
                GlobalCooldown = globalCoolDown,
                Description = description
            };

            defaultCommand = await RegisterDefaultCommand(defaultCommand);
            CommandHandler.AddCommand(defaultCommand, baseCommandService);
        }
    }
}
