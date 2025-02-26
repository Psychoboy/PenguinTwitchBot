using DotNetTwitchBot.Application.Alert.Notification;
using DotNetTwitchBot.Bot.Alerts;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Extensions;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class Defuse(
        //ILoyaltyFeature loyaltyFeature,
        IPointsSystem pointsSystem,
        IServiceBackbone serviceBackbone,
        IViewerFeature viewerFeature,
        IMediator mediator,
        ILogger<Defuse> logger,
        ICommandHandler commandHandler
            ) : BaseCommandService(serviceBackbone, commandHandler, "Defuse"), IHostedService
    {
        private readonly List<string> Wires = ["red", "blue", "yellow"];
        private readonly int Cost = 500;

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

            if (string.IsNullOrEmpty(e.Arg) ||
            (!e.Arg.Equals("red", StringComparison.CurrentCultureIgnoreCase) &&
            !e.Arg.Equals("blue", StringComparison.CurrentCultureIgnoreCase) &&
            !e.Arg.Equals("yellow", StringComparison.CurrentCultureIgnoreCase)))
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, string.Format("you need to choose one of these wires to cut: {0}", string.Join(", ", Wires)));
                throw new SkipCooldownException();
            }

            if (!(await pointsSystem.RemovePointsFromUserByUserIdAndGame(e.UserId, ModuleName, Cost)))
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, string.Format("Sorry it costs {0} to defuse the bomb which you do not have.", Cost));
                throw new SkipCooldownException();
            }

            var chosenWire = Wires.RandomElementOrDefault();
            if (chosenWire == null)
            {
                logger.LogError("Couldn't choose a wire for defuse");
                throw new SkipCooldownException();
            }
            var startMessage = string.Format("The bomb is beeping and {0} cuts the {1} wire... ", await viewerFeature.GetNameWithTitle(e.Name), e.Arg);
            if (chosenWire.Equals(e.Arg, StringComparison.CurrentCultureIgnoreCase))
            {
                var multiplier = 3;
                var min = Cost * multiplier - Cost / multiplier;
                var max = Cost * multiplier + Cost / multiplier;
                var value = Tools.Next(min, max + 1);
                await pointsSystem.AddPointsByUserIdAndGame(e.UserId, ModuleName, value);
                await ServiceBackbone.SendChatMessage(startMessage + string.Format("The bomb goes silent. As a thank for saving the day you got awarded {0} pasties", value));

                await mediator.Publish(new QueueAlert(new AlertImage().Generate("defuse.gif,8")));
            }
            else
            {
                await ServiceBackbone.SendChatMessage(startMessage + string.Format("BOOM!!! The bomb explodes, you lose {0} pasties.", Cost));
                await mediator.Publish(new QueueAlert(new AlertImage().Generate("detonated.gif,10")));
            }
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