using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Events.Chat;
using System.Timers;
using Timer = System.Timers.Timer;

namespace DotNetTwitchBot.Bot.Commands.TicketGames
{
    public class AddActive : BaseCommandService, IHostedService
    {
        //private readonly ITicketsFeature _ticketsFeature;
        protected readonly Timer _pointsToActiveCommandTimer;
        private long _pointsToGiveOut = 0;
        private DateTime _lastPointsGivenOut = DateTime.Now;
        private readonly ILogger<AddActive> _logger;
        private readonly IPointsSystem _pointsSystem;

        public AddActive(
            ILogger<AddActive> logger,
            IServiceBackbone serviceBackbone,
            IPointsSystem pointSystem,
            ICommandHandler commandHandler
        ) : base(serviceBackbone, commandHandler, "AddActive")
        {
            _logger = logger;
            _pointsToActiveCommandTimer = new Timer(1000);
            _pointsToActiveCommandTimer.Elapsed += OnActiveCommandTimerElapsed;
            _pointsSystem = pointSystem;
        }

        public override async Task Register()
        {
            var moduleName = "AddActive";
            await RegisterDefaultCommand("addactive", this, moduleName, Rank.Streamer);
            _logger.LogInformation("Registered commands for {moduleName}", moduleName);
            _pointsToActiveCommandTimer.Start();
        }

        public override Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommandDefaultName(e.Command);
            switch (command)
            {
                case "addactive":
                    {
                        if (long.TryParse(e.Args[0], out long amount))
                        {
                            AddActivePoints(amount);
                        }
                        break;
                    }
            }
            return Task.CompletedTask;
        }

        public void AddActivePoints(long amount)
        {
            if (amount > 100) amount = 100;
            _lastPointsGivenOut = GetDateTime();
            _pointsToGiveOut += amount;
        }

        protected virtual DateTime GetDateTime()
        {
            return DateTime.Now;
        }

        private async void OnActiveCommandTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            await SendTickets();
        }

        public async Task SendTickets()
        {
            if (_pointsToGiveOut > 0 && _lastPointsGivenOut.AddSeconds(5) < GetDateTime())
            {
                var pointType = await _pointsSystem.GetPointTypeForGame(ModuleName);
                await _pointsSystem.AddPointsToActiveUsers(pointType.Id.GetValueOrDefault(), _pointsToGiveOut);
                await ServiceBackbone.SendChatMessage(string.Format("Sending {0:n0} tickets to all active users.", _pointsToGiveOut));
                _pointsToGiveOut = 0;
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting {moduledname}", ModuleName);
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopped {moduledname}", ModuleName);
            return Task.CompletedTask;
        }
    }
}