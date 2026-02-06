using DotNetTwitchBot.Application.Alert.Notification;
using DotNetTwitchBot.Bot.Alerts;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Commands.Games;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Extensions;
using MediatR;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("DotNetTwitchBot.Test")]
namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class Defuse(
        IPointsSystem pointsSystem,
        IServiceBackbone serviceBackbone,
        IGameSettingsService gameSettingsService,
        IViewerFeature viewerFeature,
        IMediator mediator,
        ILogger<Defuse> logger,
        ICommandHandler commandHandler
            ) : BaseCommandService(serviceBackbone, commandHandler, GAMENAME, mediator), IHostedService
    {
        //For Game Settings
        public static readonly string GAMENAME = "Defuse";
        public static readonly string WIRES = "Wires";
        public static readonly string NO_ARGS = "NoArgs";
        public static readonly string NOT_ENOUGH = "NotEnough";
        public static readonly string STARTING = "Starting";
        public static readonly string SUCCESS = "Success";
        public static readonly string FAIL = "Failure";
        public static readonly string COST = "Cost";
        public static readonly string WIN_MULTIPLIER = "WinMultiplier";

        internal ITools Tools { get; set; } = new Tools();


        public override async Task Register()
        {
            var moduleName = "Defuse";
            await RegisterDefaultCommand("defuse", this, moduleName, userCooldown: 10);
            await pointsSystem.RegisterDefaultPointForGame(ModuleName);
            logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            if (!command.CommandProperties.CommandName.Equals("defuse")) return;
            var wires = await GetWires();
            if (string.IsNullOrEmpty(e.Arg) || !wires.Contains(e.Arg.Trim()))
            {
                var noArgMessage = await gameSettingsService.GetStringSetting(GAMENAME, NO_ARGS, "you need to choose one of these wires to cut: {Wires}");
                noArgMessage = await ReplaceVariables(noArgMessage, e.Name, "", 0, wires);
                await ServiceBackbone.ResponseWithMessage(e, noArgMessage);
                throw new SkipCooldownException();
            }

            var cost = await GetCost();

            if (!(await pointsSystem.RemovePointsFromUserByUserIdAndGame(e.UserId, e.Platform, ModuleName, cost)))
            {
                var notEnough = await gameSettingsService.GetStringSetting(GAMENAME, NOT_ENOUGH, "Sorry it costs {Cost} {PointType} to defuse the bomb which you do not have.");
                notEnough = await ReplaceVariables(notEnough, e.Name, "", cost, wires);
                await ServiceBackbone.ResponseWithMessage(e, notEnough);
                throw new SkipCooldownException();
            }
            await RunGame(e, wires, cost);
        }

        internal async Task RunGame(CommandEventArgs e, List<string> wires, int cost)
        {
            var chosenWire = wires.RandomElement();
            var nameWithTitle = await viewerFeature.GetNameWithTitle(e.Name, e.Platform);
            var starting = await gameSettingsService.GetStringSetting(GAMENAME, STARTING, "The bomb is beeping and {Name} cuts the {Wire} wire... ");
            starting = await ReplaceVariables(starting, nameWithTitle, e.Arg, cost, wires);
            var startMessage = starting;
            if (chosenWire.Equals(e.Arg, StringComparison.OrdinalIgnoreCase))
            {
                var multiplier = 3;
                var min = cost * multiplier - cost / multiplier;
                var max = cost * multiplier + cost / multiplier;
                var value = Tools.Next(min, max + 1);
                await pointsSystem.AddPointsByUserIdAndGame(e.UserId, e.Platform, ModuleName, value);
                var success = await gameSettingsService.GetStringSetting(GAMENAME, SUCCESS, "The bomb goes silent. As a thank for saving the day you got awarded {Points} {PointType}");
                success = await ReplaceVariables(success, nameWithTitle, e.Arg, value, wires);
                await ServiceBackbone.SendChatMessage(startMessage + success, e.Platform);

                await mediator.Publish(new QueueAlert(new AlertImage().Generate("defuse.gif,8")));
            }
            else
            {
                var fail = await gameSettingsService.GetStringSetting(GAMENAME, FAIL, "BOOM!!! The bomb explodes, you lose {Points} {PointType}.");
                fail = await ReplaceVariables(fail, nameWithTitle, e.Arg, cost, wires);
                await ServiceBackbone.SendChatMessage(startMessage + fail, e.Platform);
                await mediator.Publish(new QueueAlert(new AlertImage().Generate("detonated.gif,10")));
            }
        }

        private async Task<string> ReplaceVariables(string msg, string name, string wire, int points, List<string> wires)
        {
            return msg
                .Replace("{Wires}", string.Join(", ", wires), StringComparison.OrdinalIgnoreCase)
                .Replace("{Wire}", wire, StringComparison.OrdinalIgnoreCase)
                .Replace(GameSettingsService.COST, (await GetCost()).ToString("N0"), StringComparison.OrdinalIgnoreCase)
                .Replace(GameSettingsService.POINT_TYPE,(await pointsSystem.GetPointTypeForGame(ModuleName)).Name, StringComparison.OrdinalIgnoreCase)
                .Replace(GameSettingsService.NAME, name, StringComparison.OrdinalIgnoreCase)
                .Replace(GameSettingsService.POINTS, points.ToString("N0"), StringComparison.OrdinalIgnoreCase);
        }

        private Task<int> GetCost()
        {
            return gameSettingsService.GetIntSetting(
                Defuse.GAMENAME, Defuse.COST, 500
            );
        }

        private Task<List<string>> GetWires()
        {
            return gameSettingsService.GetStringListSetting(GAMENAME, WIRES, ["red", "blue", "yellow"]);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Started {moduledname}", ModuleName);
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopped {moduledname}", ModuleName);
            return Task.CompletedTask;
        }
    }
}