using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Commands.Games;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class FFA : BaseCommandService, IHostedService
    {
        private readonly List<string> Entered = [];
        private readonly Timer _joinTimer;
        private readonly IPointsSystem _pointsSystem;

        private readonly IViewerFeature _viewFeature;
        private readonly ILogger<FFA> _logger;
        private readonly IGameSettingsService _gameSettingsService;
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
            IGameSettingsService gameSettingsService
            ) : base(serviceBackbone, commandHandler, GAMENAME)
        {
            _joinTimer = new Timer(JoinTimerCallback, this, Timeout.Infinite, Timeout.Infinite);
            _pointsSystem = pointsSystem;
            _viewFeature = viewerFeature;
            _logger = logger;
            _gameSettingsService = gameSettingsService;
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
            var cost = await _gameSettingsService.GetIntSetting(ModuleName, COST, 100);
            GameState = State.Finishing;
            if (Entered.Count == 1)
            {
                await _pointsSystem.AddPointsByUserIdAndGame(Entered[0], ModuleName, cost);
                var notEnoughMessage = await _gameSettingsService.GetStringSetting(ModuleName, NOT_ENOUGH_PLAYERS, "Not enough viewers joined the FFA, returning the fees.");
                await ServiceBackbone.SendChatMessage(notEnoughMessage);
                await CleanUp();
                return;
            }

            var winnerIndex = Tools.Next(0, Entered.Count - 1);
            var winner = Entered[winnerIndex];
            var winnings = Entered.Count * cost;
            var winnerMessage = await _gameSettingsService.GetStringSetting(ModuleName, WINNER_MESSAGE, "The dust finally settled and the last one standing is {Name} and gets {Points} {PointType}!");
            var pointType = await _pointsSystem.GetPointTypeForGame(ModuleName);
            winnerMessage = winnerMessage
                .Replace("{Name}", winner, StringComparison.OrdinalIgnoreCase)
                .Replace("{Points}", winnings.ToString("N0"), StringComparison.OrdinalIgnoreCase)
                .Replace("{PointType}", pointType.Name, StringComparison.OrdinalIgnoreCase);
            await ServiceBackbone.SendChatMessage(winnerMessage);
            await _pointsSystem.AddPointsByUserIdAndGame(winner, ModuleName, winnings);
            await CleanUp();
        }

        private async Task CleanUp()
        {
            Entered.Clear();
            GameState = State.NotRunning;
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

            if (GameState == State.Finishing)
            {
                var late = await _gameSettingsService.GetStringSetting(ModuleName, LATE, "The FFA has already started, you are to late to join this one.");
                await ServiceBackbone.SendChatMessage(e.DisplayName, late);
                throw new SkipCooldownException();
            }

            if (Entered.Contains(e.Name))
            {
                var alreadyJoined = await _gameSettingsService.GetStringSetting(ModuleName, ALREADY_JOINED, "You have already joined the FFA!");
                await ServiceBackbone.SendChatMessage(e.DisplayName, alreadyJoined);
                throw new SkipCooldownException();
            }

            var cost = await _gameSettingsService.GetIntSetting(ModuleName, COST, 100);

            if (!(await _pointsSystem.RemovePointsFromUserByUserIdAndGame(e.UserId, ModuleName, cost)))
            {
                var notEnoughPoints = await _gameSettingsService.GetStringSetting(ModuleName, NOT_ENOUGH_POINTS, "Sorry it costs {Cost} {PointType} to join the FFA game which you do not have.");
                notEnoughPoints = notEnoughPoints
                    .Replace("{Cost}", cost.ToString("N0"), StringComparison.OrdinalIgnoreCase)
                    .Replace("{PointType}", (await _pointsSystem.GetPointTypeForGame(ModuleName)).Name, StringComparison.OrdinalIgnoreCase);
                await ServiceBackbone.SendChatMessage(e.DisplayName, notEnoughPoints);
                throw new SkipCooldownException();
            }

            if (GameState == State.NotRunning)
            {
                var starting = await _gameSettingsService.GetStringSetting(ModuleName, STARTING, "{Name} is starting a FFA battle! Type !{CommandName} to join now!");
                starting = starting
                    .Replace("{Name}", e.DisplayName, StringComparison.OrdinalIgnoreCase)
                    .Replace("{CommandName}", e.Command, StringComparison.OrdinalIgnoreCase);
                await ServiceBackbone.SendChatMessage(starting);
                GameState = State.Running;
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