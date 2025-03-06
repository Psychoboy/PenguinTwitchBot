using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using MediatR;
using System;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class AtSenderTagHandler : IRequestHandler<AtSenderTag, CustomCommandResult>
    {
        public Task<CustomCommandResult> Handle(AtSenderTag request, CancellationToken cancellationToken)
        {
            var result = "";
            if (request.CommandEventArgs.IsDiscord)
            {
                result = request.CommandEventArgs.DiscordMention;
            }
            else
            {
                result = string.Format("@{0}, ", request.CommandEventArgs.DisplayName);
            }
            return Task.FromResult(new CustomCommandResult(result));
        }
    }
}
