using DotNetTwitchBot.Bot.Alerts;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class Defuse(
        ILoyaltyFeature loyaltyFeature,
        IServiceBackbone serviceBackbone,
        IViewerFeature viewerFeature,
        ISendAlerts sendAlerts,
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

            if (!(await loyaltyFeature.RemovePointsFromUser(e.Name, Cost)))
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, string.Format("Sorry it costs {0} to defuse the bomb which you do not have.", Cost));
                throw new SkipCooldownException();
            }

            var chosenWire = Wires.RandomElement();
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
                await loyaltyFeature.AddPointsToViewer(e.Name, value);
                await ServiceBackbone.SendChatMessage(startMessage + string.Format("The bomb goes silent. As a thank for saving the day you got awarded {0} pasties", value));
                sendAlerts.QueueAlert("defuse.gif,8");
            }
            else
            {
                await ServiceBackbone.SendChatMessage(startMessage + string.Format("BOOM!!! The bomb explodes, you lose {0} pasties.", Cost));
                sendAlerts.QueueAlert("detonated.gif,10");
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopped {moduledname}", ModuleName);
            return Task.CompletedTask;
        }
    }
}