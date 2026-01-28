using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class ExecuteElevatedCommandTagHandler(ILogger<ExecuteCommandTagHandler> logger, IServiceBackbone serviceBackbone) : IRequestHandler<ExecuteElevatedCommandTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(ExecuteElevatedCommandTag request, CancellationToken cancellationToken)
        {
            var args = request.Args;
            var eventArgs = request.CommandEventArgs;
            if (string.IsNullOrWhiteSpace(args))
            {
                logger.LogWarning("Missing args for custom command of 'elevatedcommand' type.");
                return new CustomCommandResult();
            }

            var commandArgs = args.Split(' ');
            if (commandArgs.Length < 2)
            {
                logger.LogWarning("Missing required args for custom command of 'elevatedcommand' type.");
                return new CustomCommandResult();
            }
            var commandName = commandArgs[0];
            var commandPermission = commandArgs[1];
            var newCommandArgs = new List<string>();

            var targetUser = "";
            if (commandArgs.Length > 2)
            {
                newCommandArgs.AddRange(commandArgs.Skip(2));
                targetUser = commandArgs[2];
            }

            var command = new CommandEventArgs
            {
                Command = commandName,
                Arg = string.Join(" ", newCommandArgs),
                Args = newCommandArgs,
                TargetUser = targetUser,
                IsWhisper = eventArgs.IsWhisper,
                IsDiscord = eventArgs.IsDiscord,
                DiscordMention = eventArgs.DiscordMention,
                FromAlias = eventArgs.FromAlias,
                IsSub = commandPermission.Equals("sub") || eventArgs.IsSub,
                IsMod = commandPermission.Equals("mod") || eventArgs.IsMod,
                IsVip = commandPermission.Equals("vip") || eventArgs.IsVip,
                IsBroadcaster = commandPermission.Equals("broadcaster") || eventArgs.IsBroadcaster,
                DisplayName = eventArgs.DisplayName,
                Name = eventArgs.Name,
                SkipLock = true,
                Platform = eventArgs.Platform,
            };

            await serviceBackbone.RunCommand(command);
            return new CustomCommandResult();
        }
    }
}
