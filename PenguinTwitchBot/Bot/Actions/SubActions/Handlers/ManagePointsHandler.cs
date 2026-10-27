using System.Collections.Concurrent;
using System.Globalization;
using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public sealed class ManagePointsHandler(IPointsSystem pointsSystem, IViewerFeature viewerFeature) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.ManagePoints;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not ManagePointsType managePoints)
                throw new SubActionHandlerException(subAction, "Invalid sub action type for ManagePointsHandler");

            var pointTypeName = VariableReplacer.ReplaceVariables(managePoints.PointTypeName, variables).Trim();
            var pointType = await pointsSystem.GetPointTypeByName(pointTypeName);
            if (pointType == null || pointType.Id == Guid.Empty)
                throw new SubActionUserFacingException(subAction, "Point type not found: {0}", pointTypeName);
            var pointTypeId = pointType.Id;

            if (!Enum.TryParse<ManagePointsOperation>(managePoints.Operation, true, out var operation))
                throw new SubActionUserFacingException(subAction, "Unknown points operation: {0}", managePoints.Operation);

            var amountText = VariableReplacer.ReplaceVariables(managePoints.Amount, variables).Trim();
            if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amountValue) ||
                amountValue != decimal.Truncate(amountValue) ||
                amountValue > long.MaxValue || amountValue < long.MinValue)
                throw new SubActionUserFacingException(subAction, "Amount must evaluate to a whole number: {0}", amountText);

            var amount = (long)amountValue;
            if (operation == ManagePointsOperation.Set ? amount < 0 : amount <= 0)
                throw new SubActionUserFacingException(subAction, "Amount must be {0} for {1}.", operation == ManagePointsOperation.Set ? "zero or greater" : "greater than zero", operation);

            if (operation == ManagePointsOperation.AddActive)
            {
                await pointsSystem.AddPointsToActiveUsers(pointTypeId, amount);
                return;
            }

            var targetText = VariableReplacer.ReplaceVariables(managePoints.TargetUser, variables).Trim();
            if (string.IsNullOrWhiteSpace(targetText) || targetText.StartsWith('%') && targetText.EndsWith('%'))
                throw new SubActionUserFacingException(subAction, "Target user could not be resolved from the action variables.");

            var targetViewer = await viewerFeature.GetViewerByUserId(targetText)
                ?? await viewerFeature.GetViewerByUserName(targetText);
            if (targetViewer == null)
                throw new SubActionUserFacingException(subAction, "Target viewer not found: {0}", targetText);

            switch (operation)
            {
                case ManagePointsOperation.Add:
                    if (await pointsSystem.AddPointsByUserId(targetViewer.UserId, pointTypeId, amount) <= 0)
                        throw new SubActionUserFacingException(subAction, "Could not add {1} points to {0}.", targetViewer.Username, amount);
                    break;
                case ManagePointsOperation.Remove:
                    if (!await pointsSystem.RemovePointsFromUserByUserId(targetViewer.UserId, pointTypeId, amount))
                        throw new SubActionUserFacingException(subAction, "{0} does not have enough points to remove {1}.", targetViewer.Username, amount);
                    break;
                case ManagePointsOperation.Set:
                    await pointsSystem.SetPointsByUserId(targetViewer.UserId, pointTypeId, amount);
                    break;
                default:
                    throw new SubActionUserFacingException(subAction, "Unsupported points operation: {0}", operation);
            }
        }
    }
}