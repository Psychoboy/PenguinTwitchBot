using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Commands.Features;
using System.Collections.Concurrent;
using DotNetTwitchBot.Bot.Core.Points;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class GiftPointsHandler(ILogger<GiftPointsHandler> logger, IPointsSystem pointsSystem, IViewerFeature viewerFeature) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.GiftPoints;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables)
        {
            if(subAction is not GiftPointsType giftPoints)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for GiftPointsHandler");
            }

            var pointTypeName = VariableReplacer.ReplaceVariables(giftPoints.Text, variables);
            var pointType = await pointsSystem.GetPointTypeByName(pointTypeName) ?? throw new SubActionHandlerException(subAction, $"Invalid point type: {pointTypeName}");
            var amountString = VariableReplacer.ReplaceVariables(giftPoints.Amount, variables);
            if(string.IsNullOrEmpty(amountString) || !int.TryParse(amountString, out var amount))
            {
                throw new SubActionHandlerException(subAction, $"Invalid amount: {amountString}");
            }
            
            var fromName = VariableReplacer.ReplaceVariables(giftPoints.FromUsername, variables);
            if(string.IsNullOrWhiteSpace(fromName))
            {
                throw new SubActionHandlerException(subAction, "From Username is required.");
            }
            
            var targetName = VariableReplacer.ReplaceVariables(giftPoints.TargetName, variables);
            if(string.IsNullOrWhiteSpace(targetName))
            {
                throw new SubActionHandlerException(subAction, "Target Username is required.");
            }

            if(fromName.Equals(targetName, StringComparison.OrdinalIgnoreCase))
            {
                throw new SubActionHandlerException(subAction, "Cannot gift points to self.");
            }

            if(amount <= 0)
            {
                throw new SubActionHandlerException(subAction, "Amount must be greater than zero.");
            }

            var target = await viewerFeature.GetViewerByUserName(targetName) ?? throw new SubActionHandlerException(subAction, $"Target user not found: {targetName}");
            var from = await viewerFeature.GetViewerByUserName(fromName) ?? throw new SubActionHandlerException(subAction, $"From user not found: {fromName}");
            if(pointType.Id == null)
            {
                throw new SubActionHandlerException(subAction, $"Point type ID is null for point type: {pointType.Name}");
            }
            if (await pointsSystem.RemovePointsFromUserByUserId(from.UserId, pointType.Id.Value, amount))
            {
                await pointsSystem.AddPointsByUserId(target.UserId, pointType.Id.Value, amount);
                logger.LogInformation("Gifted {Amount} {PointType} from {From} to {Target}", amount, pointType.Name, from.Username, target.Username);
            }
            else
            {
                throw new SubActionHandlerException(subAction, $"Not enough points {from.Username}");
            }

        }
    }
}
