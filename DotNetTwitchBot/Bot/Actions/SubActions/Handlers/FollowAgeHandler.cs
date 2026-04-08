using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Commands.Features;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class FollowAgeHandler(IViewerFeature viewerFeature) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.Followage;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables)
        {
            if(subAction is not FollowAgeType followAgeType)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for FollowAgeHandler: {SubActionType}", subAction.GetType().Name);
            }

            followAgeType.Text = VariableReplacer.ReplaceVariables(followAgeType.Text, variables); //TODO: Make sure to populate args with target user or self if no args
            var follower = await viewerFeature.GetFollowerAsync(followAgeType.Text);
            if(follower == null)
            {
                variables["followage"] = $"{followAgeType.Text} is not a follower";
                variables["followage_date"] = "N/A";
            } else
            {
                variables["followage"] = string.Format("{0} has been following since {1} ({2} days ago).",
                    follower.DisplayName,
                    follower.FollowDate.ToLongDateString(),
                    Convert.ToInt32((DateTime.Now - follower.FollowDate).TotalDays));
                variables["followage_date"] = follower.FollowDate.ToLongDateString();
            }
        }
    }
}
