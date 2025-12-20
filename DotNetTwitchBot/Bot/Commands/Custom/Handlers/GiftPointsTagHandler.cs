using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using MediatR;
using System;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class GiftPointsTagHandler(
        ILogger<GiftPointsTagHandler> logger, 
        IPointsSystem pointsSystem, 
        IServiceBackbone serviceBackbone, 
        IViewerFeature viewerFeature) : IRequestHandler<GiftPointsTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(GiftPointsTag request, CancellationToken cancellationToken)
        {
            var args = request.Args;
            var eventArgs = request.CommandEventArgs;
            if (string.IsNullOrWhiteSpace(args))
            {
                logger.LogWarning("Missing args for custom command of 'giftpoints' type. Requires 1");
                return new CustomCommandResult();
            }

            var commandArgs = args.Split(" ");
            if (commandArgs.Length < 1)
            {
                logger.LogWarning("Missing required args for custom command of 'giftpoints' type. Requires 1 PointTypeId");
                return new CustomCommandResult();
            }

            if (int.TryParse(commandArgs[0], out var pointTypeId) == false)
            {
                logger.LogWarning("Invalid PointTypeId for custom command of 'giftpoints' type.");
                return new CustomCommandResult();
            }

            var pointSystem = await pointsSystem.GetPointTypeById(pointTypeId);
            if (pointSystem == null)
            {
                logger.LogWarning("Invalid PointTypeId for custom command of 'giftpoints' type.");
                return new CustomCommandResult();
            }

            if (eventArgs.Args.Count < 2)
            {
                logger.LogWarning("Missing required args for custom command of 'giftpoints' type. Requires 2");
                await serviceBackbone.ResponseWithMessage(eventArgs, $"{eventArgs.Command} Requires amount and target");
                return new CustomCommandResult();
            }

            var targetName = eventArgs.TargetUser;
            if (targetName.Equals(eventArgs.Name, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Cannot gift points to self");
                return new CustomCommandResult();
            }

            if (int.TryParse(eventArgs.Args[1], out var amount) == false)
            {
                logger.LogWarning("Invalid amount for custom command of 'giftpoints' type.");
                return new CustomCommandResult();
            }

            if (amount <= 0)
            {
                logger.LogWarning("Invalid amount for custom command of 'giftpoints' type.");
                await serviceBackbone.ResponseWithMessage(eventArgs, "Invalid amount for custom command of 'gift' type. Must be greater than 0.");
                return new CustomCommandResult();
            }

            var target = await viewerFeature.GetViewerByUserName(targetName);
            if (target == null)
            {
                logger.LogWarning("Invalid target for custom command of 'giftpoints' type.");
                await serviceBackbone.ResponseWithMessage(eventArgs, $"User {targetName} not found.");
                return new CustomCommandResult();
            }

            if (await pointsSystem.RemovePointsFromUserByUserId(eventArgs.UserId, pointTypeId, amount) == false)
            {
                logger.LogWarning("Not enough points for custom command of 'giftpoints' type.");
                await serviceBackbone.ResponseWithMessage(eventArgs, $"You don't have enough {pointSystem.Name} to gift {amount} to {targetName}.");
                return new CustomCommandResult();
            }
            await pointsSystem.AddPointsByUserId(target.UserId, pointTypeId, amount);
            await serviceBackbone.ResponseWithMessage(eventArgs, $"You have gifted {amount:N0} {pointSystem.Name} to {target.DisplayName}.");
            return new CustomCommandResult();
        }
    }
}
