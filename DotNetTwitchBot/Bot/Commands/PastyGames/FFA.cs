using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class FFA : BaseCommandService, IHostedService
    {
        private readonly int Cooldown = 300;
        private readonly int JoinTime = 180;
        private readonly int Cost = 100;
        private readonly List<string> Entered = [];
        private readonly Timer _joinTimer;
        private readonly ILoyaltyFeature _loyaltyFeature;
        private readonly IViewerFeature _viewFeature;
        private readonly ILogger<FFA> _logger;
        readonly string CommandName = "ffa";

        enum State
        {
            NotRunning,
            Running,
            Finishing
        }

        private State GameState { get; set; }

        public FFA(
            ILoyaltyFeature loyaltyFeature,
            IServiceBackbone serviceBackbone,
            ILogger<FFA> logger,
            IViewerFeature viewerFeature,
            ICommandHandler commandHandler
            ) : base(serviceBackbone, commandHandler, "FFA")
        {
            _joinTimer = new Timer(JoinTimerCallback, this, Timeout.Infinite, Timeout.Infinite);
            _loyaltyFeature = loyaltyFeature;
            _viewFeature = viewerFeature;
            _logger = logger;
        }

        private static void JoinTimerCallback(object? state)
        {
            if (state == null) return;
            var ffa = (FFA)state;
            var result = ffa.Finish();
            result.Wait();
        }

        private async Task Finish()
        {
            GameState = State.Finishing;
            if (Entered.Count == 1)
            {
                await _loyaltyFeature.AddPointsToViewerByUsername(Entered[0], Cost);
                await ServiceBackbone.SendChatMessage("Not enough viewers joined the FFA, returning the fees.");
                await CleanUp();
                return;
            }

            var winnerIndex = Tools.Next(0, Entered.Count - 1);
            var winner = Entered[winnerIndex];
            var winnings = Entered.Count * Cost;
            await ServiceBackbone.SendChatMessage(string.Format("The dust finally settled and the last one standing is {0}", await _viewFeature.GetNameWithTitle(winner)));
            await _loyaltyFeature.AddPointsToViewerByUsername(winner, winnings);
            await CleanUp();
        }

        private async Task CleanUp()
        {
            Entered.Clear();
            GameState = State.NotRunning;
            _joinTimer.Change(Timeout.Infinite, Timeout.Infinite);
            await CommandHandler.AddGlobalCooldown(CommandName, Cooldown);
        }

        public override async Task Register()
        {
            var moduleName = "FFA";
            await RegisterDefaultCommand(CommandName, this, moduleName);
            _logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {

            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            if (!command.CommandProperties.CommandName.Equals(CommandName)) return;

            if (GameState == State.Finishing)
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, "Sorry you were to late to join this one");
                throw new SkipCooldownException();
            }

            if (Entered.Contains(e.Name))
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, "You have already joined the FFA!");
                throw new SkipCooldownException();
            }

            if (!(await _loyaltyFeature.RemovePointsFromUserByUserId(e.UserId, Cost)))
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, string.Format("Sorry it costs {0} to enter the FFA, which you do not have.", Cost));
                throw new SkipCooldownException();
            }

            if (GameState == State.NotRunning)
            {
                await ServiceBackbone.SendChatMessage(string.Format("{0} is starting a FFA battle! Type !ffa to join now!", e.DisplayName));
                GameState = State.Running;
                _joinTimer.Change(JoinTime * 1000, Timeout.Infinite);
            }
            else
            {
                await ServiceBackbone.SendChatMessage(string.Format("{0} joined the FFA", e.DisplayName));
            }
            Entered.Add(e.Name);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Started {moduledname}", ModuleName);
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopped {moduledname}", ModuleName);
            return Task.CompletedTask;
        }
    }
}