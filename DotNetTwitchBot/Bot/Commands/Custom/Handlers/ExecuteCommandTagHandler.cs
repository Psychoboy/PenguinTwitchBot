using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using MediatR;
using System;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class ExecuteCommandTagHandler(ILogger<ExecuteCommandTag> logger, IServiceBackbone serviceBackbone) : IRequestHandler<ExecuteCommandTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(ExecuteCommandTag request, CancellationToken cancellationToken)
        {
            var args = request.Args;
            var eventArgs = request.CommandEventArgs;
            if (string.IsNullOrWhiteSpace(args))
            {
                logger.LogWarning("Missing args for custom command of 'command' type.");
                return new CustomCommandResult();
            }
            var commandArgs = args.Split(' ');
            var commandName = commandArgs[0];
            var newCommandArgs = new List<string>();
            var targetUser = "";
            if (commandArgs.Length > 1)
            {
                newCommandArgs.AddRange(commandArgs.Skip(1));
                targetUser = commandArgs[1];
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
                IsSub = eventArgs.IsSub,
                IsMod = eventArgs.IsMod,
                IsVip = eventArgs.IsVip,
                IsBroadcaster = eventArgs.IsBroadcaster,
                DisplayName = eventArgs.DisplayName,
                Name = eventArgs.Name,
                SkipLock = true
            };
            await serviceBackbone.RunCommand(command);
            return new CustomCommandResult();
        }
    }
}
