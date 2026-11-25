using PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations;
using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Bot.Events.Chat;

namespace PenguinTwitchBot.Bot.Commands.PastyGames
{
    public class FFA : BaseCommandService, IHostedService
    {
        private readonly List<CommandEventArgs> Entered = [];
        private readonly object _stateLock = new();
        private readonly Timer _joinTimer;
        private readonly IPointsSystem _pointsSystem;

        private readonly IViewerFeature _viewFeature;
        private readonly ILogger<FFA> _logger;
        private readonly IGameSettingsService _gameSettingsService;
        private readonly IDefaultCommandTriggerService _defaultCommandTriggerService;
        readonly string CommandName = "ffa";

        public static readonly string GAMENAME = "FFA";
        public static readonly string COOLDOWN = "Cooldown";
        public static readonly string JOIN_TIME = "JoinTime";
        public  static readonly string COST = "Cost";
        public static readonly string NOT_ENOUGH_PLAYERS = "NotEnoughPlayers";
        public static readonly string WINNER_MESSAGE = "WinningMessage";
        public static readonly string STARTING = "Starting";
        public static readonly string JOINED = "Joined";
        public static readonly string LATE = "Late";
        public static readonly string ALREADY_JOINED = "AlreadyJoined";
        public static readonly string NOT_ENOUGH_POINTS = "NotEnoughPoints";


        enum State
        {
            NotRunning,
            Running,
            Finishing
        }

        private State GameState { get; set; }

        internal ITools Tools { get; set; } = new Tools();

        public FFA(
            IPointsSystem pointsSystem,
            IServiceBackbone serviceBackbone,
            ILogger<FFA> logger,
            IViewerFeature viewerFeature,
            ICommandHandler commandHandler,
            Application.Notifications.IPenguinDispatcher dispatcher,
            IGameSettingsService gameSettingsService,
            IDefaultCommandTriggerService defaultCommandTriggerService
            ) : base(serviceBackbone, commandHandler, GAMENAME, dispatcher)
        {
            _joinTimer = new Timer(JoinTimerCallback, this, Timeout.Infinite, Timeout.Infinite);
            _pointsSystem = pointsSystem;
            _viewFeature = viewerFeature;
            _logger = logger;
            _gameSettingsService = gameSettingsService;
            _defaultCommandTriggerService = defaultCommandTriggerService;
        }

        private static async void JoinTimerCallback(object? state)
        {
            if (state is not FFA ffa) return;
            try
            {
                await ffa.Finish();
            }
            catch (Exception ex)
            {
                ffa._logger.LogError(ex, "Error finishing FFA game");
                try
                {
                    await ffa.CleanUp();
                }
                catch (Exception cleanupEx)
                {
                    ffa._logger.LogError(cleanupEx, "Error cleaning up FFA game after failure");
                }
            }
        }

        private async Task Finish()
        {
            List<CommandEventArgs> entrants;
            lock (_stateLock)
            {
                if (GameState != State.Running) return;
                GameState = State.Finishing;
                entrants = [.. Entered];
            }

            if (entrants.Count == 0)
            {
                await CleanUp();
                return;
            }

            var cost = await _gameSettingsService.GetIntSetting(ModuleName, COST, 100);
            if (entrants.Count == 1)
            {
                await _pointsSystem.AddPointsByUsernameAndGame(entrants[0].Name, ModuleName, cost);
                var notEnoughMessage = await _gameSettingsService.GetStringSetting(ModuleName, NOT_ENOUGH_PLAYERS, "Not enough viewers joined the FFA, returning the fees.");
                await ServiceBackbone.SendChatMessage(notEnoughMessage);
                await CleanUp();
                return;
            }

            var winnerIndex = Tools.Next(0, entrants.Count - 1);
            var winner = entrants[winnerIndex];
            var winnings = entrants.Count * cost;
            var winnerMessage = await _gameSettingsService.GetStringSetting(ModuleName, WINNER_MESSAGE, "The dust finally settled and the last one standing is {Name} and gets {Points} {PointType}!");
            var pointType = await _pointsSystem.GetPointTypeForGame(ModuleName);
            winnerMessage = winnerMessage
                .Replace(GameSettingsService.NAME, winner.DisplayName, StringComparison.OrdinalIgnoreCase)
                .Replace(GameSettingsService.POINTS, winnings.ToString("N0"), StringComparison.OrdinalIgnoreCase)
                .Replace(GameSettingsService.POINT_TYPE, pointType.Name, StringComparison.OrdinalIgnoreCase);
            await ServiceBackbone.SendChatMessage(winnerMessage);
            await _pointsSystem.AddPointsByUsernameAndGame(winner.Name, ModuleName, winnings);
            await _defaultCommandTriggerService.TriggerDefaultCommandEventAsync(
                CommandName,
                DefaultCommandEventTypes.FfaWin,
                winner,
                new Dictionary<string, string>
                {
                    ["Winner"] = winner.DisplayName,
                    ["Winnings"] = winnings.ToString("N0"),
                    ["ParticipantCount"] = entrants.Count.ToString()
                });
            await CleanUp();
        }

        private async Task CleanUp()
        {
            lock (_stateLock)
            {
                Entered.Clear();
                GameState = State.NotRunning;
            }
            _joinTimer.Change(Timeout.Infinite, Timeout.Infinite);
            var cooldown = await _gameSettingsService.GetIntSetting(ModuleName, COOLDOWN, 300);
            await CommandHandler.AddGlobalCooldown(CommandName, cooldown);
        }

        public override async Task Register()
        {
            var moduleName = "FFA";
            await RegisterDefaultCommand(CommandName, this, moduleName);
            await _pointsSystem.RegisterDefaultPointForGame(ModuleName);
            _logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {

            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            if (!command.CommandProperties.CommandName.Equals(CommandName)) return;

            bool isFinishing;
            bool isAlreadyJoined;
            lock (_stateLock)
            {
                isFinishing = GameState == State.Finishing;
                isAlreadyJoined = Entered.Any(entrant => string.Equals(entrant.Name, e.Name, StringComparison.OrdinalIgnoreCase));
            }

            if (isFinishing)
            {
                var late = await _gameSettingsService.GetStringSetting(ModuleName, LATE, "The FFA has already started, you are to late to join this one.");
                await ServiceBackbone.ResponseWithMessage(e, late);
                throw new SkipCooldownException();
            }

            if (isAlreadyJoined)
            {
                var alreadyJoined = await _gameSettingsService.GetStringSetting(ModuleName, ALREADY_JOINED, "You have already joined the FFA!");
                await ServiceBackbone.ResponseWithMessage(e, alreadyJoined);
                throw new SkipCooldownException();
            }

            var cost = await _gameSettingsService.GetIntSetting(ModuleName, COST, 100);

            if (!(await _pointsSystem.RemovePointsFromUserByUserIdAndGame(e.UserId, ModuleName, cost)))
            {
                var notEnoughPoints = await _gameSettingsService.GetStringSetting(ModuleName, NOT_ENOUGH_POINTS, "Sorry it costs {Cost} {PointType} to join the FFA game which you do not have.");
                notEnoughPoints = notEnoughPoints
                    .Replace("{Cost}", cost.ToString("N0"), StringComparison.OrdinalIgnoreCase)
                    .Replace("{PointType}", (await _pointsSystem.GetPointTypeForGame(ModuleName)).Name, StringComparison.OrdinalIgnoreCase);
                await ServiceBackbone.ResponseWithMessage(e, notEnoughPoints);
                throw new SkipCooldownException();
            }

            bool startedNewGame = false;
            bool rejectedLate = false;
            bool rejectedDuplicate = false;
            lock (_stateLock)
            {
                if (GameState == State.Finishing)
                {
                    rejectedLate = true;
                }
                else if (Entered.Any(entrant => string.Equals(entrant.Name, e.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    rejectedDuplicate = true;
                }
                else
                {
                    if (GameState == State.NotRunning)
                    {
                        GameState = State.Running;
                        startedNewGame = true;
                    }
                    Entered.Add(e);
                }
            }

            if (rejectedLate || rejectedDuplicate)
            {
                await _pointsSystem.AddPointsByUsernameAndGame(e.Name, ModuleName, cost);
                var msg = rejectedLate
                    ? await _gameSettingsService.GetStringSetting(ModuleName, LATE, "The FFA has already started, you are to late to join this one.")
                    : await _gameSettingsService.GetStringSetting(ModuleName, ALREADY_JOINED, "You have already joined the FFA!");
                await ServiceBackbone.ResponseWithMessage(e, msg);
                throw new SkipCooldownException();
            }

            if (startedNewGame)
            {
                var starting = await _gameSettingsService.GetStringSetting(ModuleName, STARTING, "{Name} is starting a FFA battle! Type !{CommandName} to join now!");
                starting = starting
                    .Replace("{Name}", e.DisplayName, StringComparison.OrdinalIgnoreCase)
                    .Replace("{CommandName}", e.Command, StringComparison.OrdinalIgnoreCase);
                await ServiceBackbone.SendChatMessage(starting);
                var joinTime = await _gameSettingsService.GetIntSetting(ModuleName, JOIN_TIME, 180);
                _joinTimer.Change(joinTime * 1000, Timeout.Infinite);
            }
            else
            {
                var joined = await _gameSettingsService.GetStringSetting(ModuleName, JOINED, "{Name} joined the FFA!");
                joined = joined
                    .Replace("{Name}", e.DisplayName, StringComparison.OrdinalIgnoreCase);
                await ServiceBackbone.SendChatMessage(joined);
            }
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
