using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core.Points;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class CheckPointsHandler(
        ILogger<CheckPointsHandler> logger,
        IPointsSystem pointsSystem,
        IViewerFeature viewerFeature) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.CheckPoints;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables)
        {
            if (subAction is not CheckPointsType checkPointsType)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for CheckPointsHandler");
            }

            var pointType = await pointsSystem.GetPointTypeByName(checkPointsType.PointTypeName);
            if (pointType == null || !pointType.Id.HasValue)
            {
                logger.LogError("Point type not found for CheckPointsHandler");
                //Throw so the action logger finds it and it can be fixed in the UI before it causes any issues
                throw new SubActionHandlerException(subAction, $"Invalid point type: {checkPointsType.PointTypeName}");
            }

            var targetUser = VariableReplacer.ReplaceVariables(checkPointsType.TargetUser, variables);
            if (string.IsNullOrWhiteSpace(targetUser))
            {
                logger.LogError("Target user is empty after variable replacement in CheckPointsHandler");
                throw new SubActionHandlerException(subAction, "Target user cannot be empty");
            }

            var targetViewer = await viewerFeature.GetViewerByUserName(targetUser);
            if (targetViewer == null)
            {
                logger.LogError("Target viewer not found for CheckPointsHandler");
                throw new SubActionHandlerException(subAction, $"Viewer not found: {targetUser}");
            }

            var points = await pointsSystem.GetUserPointsByUserId(targetViewer.UserId, pointType.Id.Value);
            variables["TargetPoints"] = points.Points.ToString();
            variables["TargetPointsFormatted"] = $"{points.Points:N0}";
        }
    }
}