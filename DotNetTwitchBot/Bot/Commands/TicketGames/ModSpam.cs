using DotNetTwitchBot.Bot.Commands.Games;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Events.Chat;


namespace DotNetTwitchBot.Bot.Commands.TicketGames
{
    public class ModSpam(
        AddActive addActive,
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        ILogger<ModSpam> logger,
        ITools tools,
        TimeProvider timeProvider,
        IGameSettingsService gameSettingsService,
        IPointsSystem pointsSystem
            ) : BaseCommandService(serviceBackbone, commandHandler, GAMENAME), IHostedService
    {
        private ITimer? _intervalTimer;
        TimeSpan _runTime = new(0, 0, 0, 15);
        DateTime _startTime = new();

        public static readonly string GAMENAME = "ModSpam";
        public static readonly string STARTING_MESSAGE = "StartingMessage";
        public static readonly string ENDING_MESSAGE = "EndingMessage";
        public static readonly string MIN_TIME = "MinTime";
        public static readonly string MAX_TIME = "MaxTime";
        public static readonly string MIN_AMOUNT = "MinAmount";
        public static readonly string MAX_AMOUNT = "MaxAmount";

        public override async Task Register()
        {
            await RegisterDefaultCommand("modspam", this, GAMENAME, Rank.Moderator, globalCoolDown: 1200);
            logger.LogInformation("Registered commands for {moduleName}", GAMENAME);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            switch (command.CommandProperties.CommandName)
            {
                case "modspam":
                    await StartModSpam();
                    break;
            }

        }

        private async void TimerCallBack(object? state)
        {
            if (state == null) return;
            var modSpam = (ModSpam)state;
            await modSpam.UpdateOrStopSpam();
        }

        private async Task UpdateOrStopSpam()
        {
            var minAmount = await gameSettingsService.GetIntSetting(GAMENAME, MIN_AMOUNT, 1);
            var maxAmount = await gameSettingsService.GetIntSetting(GAMENAME, MAX_AMOUNT, 8);
            await addActive.AddActivePoints(tools.RandomRange(minAmount, maxAmount));
            var elapsedTime = DateTime.Now - _startTime;
            if (elapsedTime > _runTime)
            {
                _intervalTimer?.Dispose();
                var message = await gameSettingsService.GetStringSetting(GAMENAME, ENDING_MESSAGE, "Mod spam completed... {PointType} arriving soon.");
                message = message.Replace("{PointType}", (await pointsSystem.GetPointTypeForGame("AddActive")).Name, StringComparison.CurrentCultureIgnoreCase);
                await ServiceBackbone.SendChatMessage(message);


            }
        }

        private async Task StartModSpam()
        {
            var message = await gameSettingsService.GetStringSetting(GAMENAME, STARTING_MESSAGE, "Starting Mod Spam... please wait while it spams silently...");
            await ServiceBackbone.SendChatMessage(message);
            var minTime = await gameSettingsService.GetIntSetting(GAMENAME, MIN_TIME, 15);
            var maxTime = await gameSettingsService.GetIntSetting(GAMENAME, MAX_TIME, 20);
            _runTime = new TimeSpan(0, 0, tools.RandomRange(minTime, minTime));
            _startTime = DateTime.Now;
            _intervalTimer = timeProvider.CreateTimer(TimerCallBack, this, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
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