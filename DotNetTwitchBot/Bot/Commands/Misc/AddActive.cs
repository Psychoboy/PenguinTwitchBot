using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using System.Timers;
using Timer = System.Timers.Timer;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class AddActive : BaseCommandService, IHostedService
    {
        private readonly ITicketsFeature _ticketsFeature;
        protected readonly Timer _ticketsToActiveCommandTimer;
        private long _ticketsToGiveOut = 0;
        private DateTime _lastTicketsAdded = DateTime.Now;
        private readonly ILogger<AddActive> _logger;

        public AddActive(
            ILogger<AddActive> logger,
            IServiceBackbone serviceBackbone,
            ITicketsFeature ticketsFeature,
            ICommandHandler commandHandler
        ) : base(serviceBackbone, commandHandler, "AddActive")
        {
            _ticketsFeature = ticketsFeature;
            _logger = logger;
            _ticketsToActiveCommandTimer = new Timer(1000);
            _ticketsToActiveCommandTimer.Elapsed += OnActiveCommandTimerElapsed;
        }

        public override async Task Register()
        {
            var moduleName = "AddActive";
            await RegisterDefaultCommand("addactive", this, moduleName, Rank.Streamer);
            _logger.LogInformation("Registered commands for {moduleName}", moduleName);
            _ticketsToActiveCommandTimer.Start();
        }

        public override Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommandDefaultName(e.Command);
            switch (command)
            {
                case "addactive":
                    {
                        if (Int64.TryParse(e.Args[0], out long amount))
                        {
                            AddActiveTickets(amount);
                        }
                        break;
                    }
            }
            return Task.CompletedTask;
        }

        public void AddActiveTickets(long amount)
        {
            if (amount > 100) amount = 100;
            _lastTicketsAdded = GetDateTime();
            _ticketsToGiveOut += amount;
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
            if (_ticketsToGiveOut > 0 && _lastTicketsAdded.AddSeconds(5) < GetDateTime())
            {
                await _ticketsFeature.GiveTicketsToActiveUsers(_ticketsToGiveOut);
                await ServiceBackbone.SendChatMessage(string.Format("Sending {0:n0} tickets to all active users.", _ticketsToGiveOut));
                _ticketsToGiveOut = 0;
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