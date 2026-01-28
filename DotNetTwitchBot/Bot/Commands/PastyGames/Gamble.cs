using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Commands.Games;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.TwitchServices;
using DotNetTwitchBot.Repository;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class Gamble(
        ILogger<Gamble> logger,
        IGameSettingsService gameSettingsService,
        IPointsSystem pointsSystem,
        ITwitchService twitchServices,
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        IMediator mediator,
        ITools tools,
        MaxBetCalculator maxBetCalculator
            ) : BaseCommandService(serviceBackbone, commandHandler, GAMENAME, mediator), IHostedService
    {
        public static readonly string GAMENAME = "Gamble";
        
        public static readonly string JACKPOT_NUMBER = "JackpotNumber";
        public static readonly string STARTING_JACKPOT = "StartingJackpot";
        public static readonly string MINIMUM_FOR_WIN = "MinimumForWin";
        public static readonly string MINIMUM_BET = "MinimumBet";
        public static readonly string WINNING_MULTIPLIER = "WinningMultiplier";
        public static readonly string JACKPOT_CONTRIBUTION = "JackpotContribution";
        public static readonly string CURRENT_JACKPOT = "CurrentJackpot";

        public static readonly string CURRENT_JACKPOT_MESSAGE = "CurrentJackpotMessage";
        public static readonly string INCORRECT_ARGS = "IncorrectArgs";
        public static readonly string INCORRECT_BET = "IncorrectBet";
        public static readonly string NOT_ENOUGH = "NotEnough";
        public static readonly string WIN_MESSAGE = "WinMessage";
        public static readonly string LOSE_MESSAGE = "LoseMessage";
        public static readonly string JACKPOT_MESSAGE = "JackpotMessage";



        public override async Task Register()
        {
            var moduleName = "Gamble";
            await RegisterDefaultCommand("gamble", this, moduleName, Rank.Viewer, userCooldown: 180);
            await RegisterDefaultCommand("jackpot", this, moduleName, Rank.Viewer);
            await pointsSystem.RegisterDefaultPointForGame(ModuleName);
            logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            switch (command.CommandProperties.CommandName)
            {
                case "gamble":
                    await HandleGamble(e);
                    break;
                case "jackpot":
                    var jackpot = await GetJackpot();
                    var pointType = await pointsSystem.GetPointTypeForGame(ModuleName);
                    var jackpotMessage = await gameSettingsService.GetStringSetting(GAMENAME, CURRENT_JACKPOT_MESSAGE, "The current jackpot is {jackpot} {PointType}");
                    jackpotMessage = jackpotMessage
                        .Replace("{jackpot}", jackpot.ToString("N0"), StringComparison.OrdinalIgnoreCase)
                        .Replace("{PointType}", pointType.Name, StringComparison.OrdinalIgnoreCase);

                    await ServiceBackbone.ResponseWithMessage(e, jackpotMessage);
                    break;
            }
        }

        private async Task HandleGamble(CommandEventArgs e)
        {
            if (e.Args.Count == 0)
            {
                var errorMessage = await gameSettingsService.GetStringSetting(ModuleName, INCORRECT_ARGS, "To gamble, do !{Command} amount to specify amount or do !{Command} max or all to do the max bet. You can also do it by percentage like !{Command} 50%");
                errorMessage = errorMessage.Replace("{Command}", e.Command, StringComparison.OrdinalIgnoreCase);
                await ServiceBackbone.ResponseWithMessage(e, errorMessage);
                throw new SkipCooldownException();
            }
            var minBet = await gameSettingsService.GetIntSetting(ModuleName, MINIMUM_BET, 5);
            var maxBet = await maxBetCalculator.CheckAndRemovePoints(e.UserId, e.Platform, "gamble", e.Args.First(), minBet);
            long amount = 0;
            switch (maxBet.Result)
            {
                case MaxBet.ParseResult.Success:
                    amount = maxBet.Amount;
                    break;

                case MaxBet.ParseResult.InvalidValue:
                    {
                        var errorMessage = await gameSettingsService.GetStringSetting(ModuleName, INCORRECT_ARGS, "To gamble, do !{Command} amount to specify amount or do !{Command} max or all to do the max bet. You can also do it by percentage like !{Command} 50%");
                        errorMessage = errorMessage.Replace("{Command}", e.Command, StringComparison.OrdinalIgnoreCase);
                        await ServiceBackbone.ResponseWithMessage(e, errorMessage);
                        throw new SkipCooldownException();
                    }

                case MaxBet.ParseResult.ToMuch:
                case MaxBet.ParseResult.ToLow:
                    {
                        var pointType = await pointsSystem.GetPointTypeForGame(ModuleName);
                        var errorMessage = await gameSettingsService.GetStringSetting(ModuleName, INCORRECT_BET, "The max bet is {MaxBet} {PointType} and must be greater then {MinBet} {PointType}");
                        errorMessage = errorMessage
                            .Replace("{MaxBet}", LoyaltyFeature.MaxBet.ToString("N0"), StringComparison.OrdinalIgnoreCase)
                            .Replace("{MinBet}", minBet.ToString("N0"), StringComparison.OrdinalIgnoreCase)
                            .Replace("{PointType}", pointType.Name, StringComparison.OrdinalIgnoreCase);
                        await ServiceBackbone.ResponseWithMessage(e, errorMessage);
                        throw new SkipCooldownException();
                    }

                case MaxBet.ParseResult.NotEnough:
                    {
                        var errorMessage = await gameSettingsService.GetStringSetting(ModuleName, NOT_ENOUGH, "You don't have enough to gamble with.");
                        await ServiceBackbone.ResponseWithMessage(e, errorMessage);
                        throw new SkipCooldownException();
                    }
            }

            var jackpot = await GetJackpot();

            var winningMultiplier = await gameSettingsService.GetIntSetting(ModuleName, WINNING_MULTIPLIER, 2);
            var jackpotNumber = await gameSettingsService.GetIntSetting(ModuleName, JACKPOT_NUMBER, 69);
            var winRange = await gameSettingsService.GetIntSetting(ModuleName, MINIMUM_FOR_WIN, 48);
            var value = tools.Next(1, 100 + 1);

            //Checks to see if they hit the jackpot
            if (value == jackpotNumber && ServiceBackbone.IsOnline) 
            {
                var jackpotWinnings = jackpot * (amount / LoyaltyFeature.MaxBet);
                var winnings = amount * winningMultiplier;
                jackpot -= jackpotWinnings;
                var jackpotDefault = await gameSettingsService.GetIntSetting(ModuleName, STARTING_JACKPOT, 1000);
                if (jackpot < jackpotDefault)
                {
                    jackpot = jackpotDefault;
                }
                await UpdateJackpot(jackpot, true);
                var jackpotWin = await gameSettingsService.GetStringSetting(ModuleName, JACKPOT_MESSAGE, "{Name} rolled {Rolled} and won the jackpot of {Points} {PointType}!");
                jackpotWin = jackpotWin
                    .Replace("{Name}", e.DisplayName, StringComparison.OrdinalIgnoreCase)
                    .Replace("{Rolled}", value.ToString(), StringComparison.OrdinalIgnoreCase)
                    .Replace("{Points}", (winnings + jackpotWinnings).ToString("N0"), StringComparison.OrdinalIgnoreCase)
                    .Replace("{PointType}", (await pointsSystem.GetPointTypeForGame(ModuleName)).Name, StringComparison.OrdinalIgnoreCase);
                if(PlatformType.Twitch == e.Platform)
                {
                    await twitchServices.Announcement(jackpotWin);
                } else
                {
                    await ServiceBackbone.ResponseWithMessage(e, jackpotWin);
                }

                await pointsSystem.AddPointsByUserIdAndGame(e.UserId, e.Platform, ModuleName, winnings + jackpotWinnings);
                await LaunchFireworks();
            }
            //If not jackpot see if they win at all
            else if (value > winRange || value == jackpotNumber)
            {
                var winnings = amount * winningMultiplier;
                var winMessage = await gameSettingsService.GetStringSetting(ModuleName, WIN_MESSAGE, "{Name} rolled {Rolled} and won {Points} {PointType}!");
                winMessage = winMessage
                    .Replace("{Name}", e.DisplayName, StringComparison.OrdinalIgnoreCase)
                    .Replace("{Rolled}", value.ToString(), StringComparison.OrdinalIgnoreCase)
                    .Replace("{Points}", winnings.ToString("N0"), StringComparison.OrdinalIgnoreCase)
                    .Replace("{PointType}", (await pointsSystem.GetPointTypeForGame(ModuleName)).Name, StringComparison.OrdinalIgnoreCase);
                await ServiceBackbone.ResponseWithMessage(e, winMessage);
                await pointsSystem.AddPointsByUserIdAndGame(e.UserId, e.Platform, ModuleName, winnings);
                if (value == jackpotNumber)
                {
                    await ServiceBackbone.ResponseWithMessage(e, "You hit the jackpot! But the stream is offline so just normal win :(");
                }
            }
            //Otherwise they lose
            else
            {
                var jackpotContribution = await gameSettingsService.GetDoubleSetting(ModuleName, JACKPOT_CONTRIBUTION, 0.10);
                await UpdateJackpot(Convert.ToInt64(amount * jackpotContribution), false);
                var loseMessage = await gameSettingsService.GetStringSetting(ModuleName, LOSE_MESSAGE, "{Name} rolled {Rolled} and lost {Points} {PointType}");
                loseMessage = loseMessage
                    .Replace("{Name}", e.DisplayName, StringComparison.OrdinalIgnoreCase)
                    .Replace("{Rolled}", value.ToString(), StringComparison.OrdinalIgnoreCase)
                    .Replace("{Points}", amount.ToString("N0"), StringComparison.OrdinalIgnoreCase)
                    .Replace("{PointType}", (await pointsSystem.GetPointTypeForGame(ModuleName)).Name, StringComparison.OrdinalIgnoreCase);
                await ServiceBackbone.ResponseWithMessage(e, loseMessage);
            }
        }

        //Special function to launch fireworks Not for public use yet
        private static async Task LaunchFireworks()
        {
            try
            {
                var httpClient = new HttpClient();
                var request = new HttpRequestMessage
                {
                    RequestUri = new Uri("http://127.0.0.1:7474/DoAction"),
                    Method = HttpMethod.Post,
                    Content = new StringContent("{\"action\":{\"id\":\"c4a5e3b8-a607-4b34-b8fe-ff7b36c3f3d4\",\"name\":\"Fireworks - General - 50 fireworks\"},\"args\": {}}")
                };
                await httpClient.SendAsync(request);
            }
            catch (Exception)
            {
                // do nothing
            }
        }

        private async Task UpdateJackpot(long amount, bool reset)
        {
            var jackpot = await GetJackpot();
            if (reset)
            {
                jackpot = amount;
            }
            else
            {
                jackpot += amount;
            }
            await gameSettingsService.SetLongSetting(ModuleName, CURRENT_JACKPOT, jackpot);
        }

        private async Task<long> GetJackpot()
        {
            return await gameSettingsService.GetLongSetting(ModuleName, CURRENT_JACKPOT, 1000);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting {moduledname}", ModuleName);
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopped {moduledname}", ModuleName);
            return Task.CompletedTask;
        }
    }
}