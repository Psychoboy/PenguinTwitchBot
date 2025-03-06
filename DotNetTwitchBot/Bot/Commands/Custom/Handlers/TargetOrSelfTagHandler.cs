using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using MediatR;
using System;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class TargetOrSelfTagHandler : IRequestHandler<TargetOrSelfTag, CustomCommandResult>
    {
        public Task<CustomCommandResult> Handle(TargetOrSelfTag request, CancellationToken cancellationToken)
        {
            var eventArgs = request.CommandEventArgs;
            return Task.FromResult(new CustomCommandResult(string.IsNullOrWhiteSpace(eventArgs.TargetUser) ? eventArgs.Name : eventArgs.TargetUser));
        }
    }
}
