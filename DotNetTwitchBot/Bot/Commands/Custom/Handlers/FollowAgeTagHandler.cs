using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using DotNetTwitchBot.Bot.Commands.Features;
using MediatR;
using System;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class FollowAgeTagHandler(IViewerFeature viewerFeature) : IRequestHandler<FollowAgeTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(FollowAgeTag request, CancellationToken cancellationToken)
        {
            var eventArgs = request.CommandEventArgs;
            var args = request.Args;
            if (string.IsNullOrWhiteSpace(args)) args = eventArgs.Name;
            var follower = await viewerFeature.GetFollowerAsync(args, eventArgs.Platform);
            if (follower == null)
            {
                return new CustomCommandResult(string.Format("{0} is not a follower", args));
            }
            return new CustomCommandResult(string.Format("{0} has been following since {1} ({2} days ago).",
                    follower.DisplayName, 
                    follower.FollowDate.ToLongDateString(), 
                    Convert.ToInt32((DateTime.Now - follower.FollowDate).TotalDays))
                );
        }
    }
}
