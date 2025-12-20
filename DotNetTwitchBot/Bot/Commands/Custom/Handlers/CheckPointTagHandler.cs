using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using MediatR;
using System;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class CheckPointTagHandler(
        ILogger<CheckPointTagHandler> logger, 
        IPointsSystem pointsSystem,
        IViewerFeature viewerFeature,
        IServiceBackbone serviceBackbone) : IRequestHandler<CheckPointsTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(CheckPointsTag request, CancellationToken cancellationToken)
        {
            var args = request.Args;
            var eventArgs = request.CommandEventArgs;
            if (string.IsNullOrWhiteSpace(args))
            {
                logger.LogWarning("Missing args for custom command of 'checkpoints' type.");
                return new CustomCommandResult();
            }
            var commandArgs = args.Split(" ");
            if (commandArgs.Length < 1)
            {
                logger.LogWarning("Missing required args for custom command of 'checkpoints' type.");
                return new CustomCommandResult();
            }
            if (int.TryParse(commandArgs[0], out var pointTypeId) == false)
            {
                logger.LogWarning("Invalid PointTypeId for custom command of 'checkpoints' type.");
                return new CustomCommandResult();
            }
            var pointSystem = await pointsSystem.GetPointTypeById(pointTypeId);
            if (pointSystem == null)
            {
                logger.LogWarning("Invalid PointTypeId for custom command of 'checkpoints' type.");
                return new CustomCommandResult();
            }
            var targetName = eventArgs.TargetUser;
            var target = await viewerFeature.GetViewerByUserName(targetName);
            if (target == null)
            {
                logger.LogWarning("Invalid target for custom command of 'checkpoints' type.");
                await serviceBackbone.ResponseWithMessage(eventArgs, $"User {target} not found.");
                return new CustomCommandResult();
            }
            var points = await pointsSystem.GetUserPointsByUserId(target.UserId, pointTypeId);
            await serviceBackbone.ResponseWithMessage(eventArgs, $"{target.DisplayName} has {points.Points:N0} {pointSystem.Name}.");
            return new CustomCommandResult();
        }
    }
}
