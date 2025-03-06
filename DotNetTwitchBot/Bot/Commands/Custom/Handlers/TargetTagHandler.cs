using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using MediatR;
using System;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class TargetTagHandler : IRequestHandler<TargetTag, CustomCommandResult>
    {
        public Task<CustomCommandResult> Handle(TargetTag request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new CustomCommandResult(request.CommandEventArgs.TargetUser));
        }
    }
}
