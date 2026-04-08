using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using System;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class UserOnlyTagHandler : Application.Notifications.IRequestHandler<UserOnlyTag, CustomCommandResult>
    {
        public Task<CustomCommandResult> Handle(UserOnlyTag request, CancellationToken cancellationToken)
        {
            if (request.CommandEventArgs.Name.Equals(request.Args, StringComparison.OrdinalIgnoreCase)) return Task.FromResult(new CustomCommandResult());
            return Task.FromResult( new CustomCommandResult(true));
        }
    }
}
