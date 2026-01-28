using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Commands.Games;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Events.Chat;
using MediatR;
using System.Timers;
using Timer = System.Timers.Timer;

namespace DotNetTwitchBot.Bot.Commands.TicketGames
{
    public class AddActive(
        ILogger<AddActive> logger,
        IServiceBackbone serviceBackbone,
        IPointsSystem pointSystem,
        ICommandHandler commandHandler,
        IMediator mediator,
        IGameSettingsService gameSettingsService
        ) : BaseCommandService(serviceBackbone, commandHandler, GAMENAME, mediator), IHostedService
    {
        protected readonly Timer _pointsToActiveCommandTimer = new(1000);
        private long _pointsToGiveOut = 0;
        private DateTime _lastPointsGivenOut = DateTime.Now;

        public static readonly string GAMENAME = "AddActive";
        public static readonly string MAX_POINTS = "MaxPoints";
        public static readonly string DELAY = "Delay";
        public static readonly string MESSAGE = "Message";

        public override async Task Register()
        {
            var moduleName = "AddActive";
            await RegisterDefaultCommand("addactive", this, moduleName, Rank.Streamer);
            await pointSystem.RegisterDefaultPointForGame(ModuleName);
            logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommandDefaultName(e.Command);
            switch (command)
            {
                case "addactive":
                    {
                        if (long.TryParse(e.Args[0], out long amount))
                        {
                            await AddActivePoints(amount);
                        }
                        break;
                    }
            }
        }

        public async Task AddActivePoints(long amount)
        {
            var maxPoints = await gameSettingsService.GetIntSetting(ModuleName, MAX_POINTS, 100);
            if (maxPoints > 0 && amount > maxPoints) amount = maxPoints;
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
            _pointsToActiveCommandTimer.Stop();
            try
            {
                var delay = await gameSettingsService.GetIntSetting(ModuleName, DELAY, 5);
                if (_pointsToGiveOut > 0 && _lastPointsGivenOut.AddSeconds(delay) < GetDateTime())
                {
                    var pointType = await pointSystem.GetPointTypeForGame(ModuleName);
                    await pointSystem.AddPointsToActiveUsers(pointType.Id.GetValueOrDefault(), PlatformType.Twitch, _pointsToGiveOut);

                    var message = await gameSettingsService.GetStringSetting(ModuleName, MESSAGE, "Sending {Amount} {PointType} to all active users.");
                    message = message
                        .Replace("{Amount}", _pointsToGiveOut.ToString("n0"), StringComparison.OrdinalIgnoreCase)
                        .Replace("{PointType}", pointType.Name, StringComparison.OrdinalIgnoreCase);

                    await ServiceBackbone.SendChatMessage(message, PlatformType.Twitch);
                    _pointsToGiveOut = 0;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error sending tickets");
            }
            finally
            {
                _pointsToActiveCommandTimer.Start();
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting {moduledname}", ModuleName);
            _pointsToActiveCommandTimer.Elapsed += OnActiveCommandTimerElapsed;
            _pointsToActiveCommandTimer.Start();
            await Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopped {moduledname}", ModuleName);
            _pointsToActiveCommandTimer.Elapsed -= OnActiveCommandTimerElapsed;
            _pointsToActiveCommandTimer.Stop();
            return Task.CompletedTask;
        }
    }
}