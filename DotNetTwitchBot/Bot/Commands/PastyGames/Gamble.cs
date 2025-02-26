using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.TwitchServices;
using DotNetTwitchBot.Repository;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class Gamble(
        ILogger<Gamble> logger,
        ILanguage language,
        //ILoyaltyFeature loyaltyFeature,
        IPointsSystem pointsSystem,
        IServiceScopeFactory scopeFactory,
        ITwitchService twitchServices,
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        MaxBetCalculator maxBetCalculator
            ) : BaseCommandService(serviceBackbone, commandHandler, "Gamble"), IHostedService
    {
        public int JackPotNumber { get; } = 69;
        public long JackpotDefault { get; } = 1000;
        public int WinRange { get; } = 48;

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
                    await ServiceBackbone.SendChatMessage(e.DisplayName,
                    language.Get("game.jackpot.response").Replace("(jackpot)", jackpot.ToString("N0")));
                    break;
            }
        }

        private async Task HandleGamble(CommandEventArgs e)
        {
            if (e.Args.Count == 0)
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName,
                "To gamble, do !gamble amount to specify amount or do !gamble max or all to do the max bet. You can also do it by percentage like !gamble 50%");
                throw new SkipCooldownException();
            }

            var maxBet = await maxBetCalculator.CheckAndRemovePoints(e.UserId, "gamble", e.Args.First(), 5);
            long amount = 0;
            switch (maxBet.Result)
            {
                case MaxBet.ParseResult.Success:
                    amount = maxBet.Amount;
                    break;

                case MaxBet.ParseResult.InvalidValue:
                    await ServiceBackbone.SendChatMessage(e.DisplayName,
                    "To gamble, do !gamble amount to specify amount or do !gamble max or all to do the max bet. You can also do it by percentage like !gamble 50%");
                    throw new SkipCooldownException();

                case MaxBet.ParseResult.ToMuch:
                case MaxBet.ParseResult.ToLow:
                    await ServiceBackbone.SendChatMessage(e.DisplayName, string.Format("The max bet is {0} and must be greater then 5", LoyaltyFeature.MaxBet.ToString("N0")));
                    throw new SkipCooldownException();

                case MaxBet.ParseResult.NotEnough:
                    await ServiceBackbone.SendChatMessage(e.DisplayName,
                        "you don't have that much to gamble with.");
                    throw new SkipCooldownException();
            }

            var jackpot = await GetJackpot();

            var value = Tools.Next(1, 100 + 1);
            if (value == JackPotNumber)
            {
                var jackpotWinnings = jackpot * (amount / LoyaltyFeature.MaxBet);
                var winnings = amount * 2;
                jackpot -= jackpotWinnings;
                if (jackpot < JackpotDefault)
                {
                    jackpot = JackpotDefault;
                }
                await UpdateJackpot(jackpot, true);
                await twitchServices.Announcement(string.Format("{0} rolled {1} and won the jackpot of {2} pasties!", e.DisplayName, value, (winnings + jackpotWinnings).ToString("N0")));
                await LaunchFireworks();
                await pointsSystem.AddPointsByUserIdAndGame(e.UserId, ModuleName, amount);
            }
            else if (value > WinRange)
            {
                var winnings = amount * 2;
                await ServiceBackbone.SendChatMessage(string.Format("{0} rolled {1} and won the {2} pasties!", e.DisplayName, value, winnings.ToString("N0")));
                await pointsSystem.AddPointsByUserIdAndGame(e.UserId, ModuleName, winnings);
            }
            else
            {
                await UpdateJackpot(Convert.ToInt64(amount * 0.10), false);
                await ServiceBackbone.SendChatMessage(string.Format("{0} rolled {1} and lost {2} pasties", e.DisplayName, value, amount.ToString("N0")));
            }
        }

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
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var jackpotSetting = await db.Settings.Find(x => x.Name.Equals("jackpot")).FirstOrDefaultAsync();
            jackpotSetting ??= new Setting { Name = "jackpot", LongSetting = 0, DataType = Setting.DataTypeEnum.Long };
            jackpotSetting.LongSetting = jackpot;
            db.Settings.Update(jackpotSetting);
            await db.SaveChangesAsync();
        }

        private async Task<Int64> GetJackpot()
        {
            var jackpot = JackpotDefault;
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var jackpotSetting = await db.Settings.Find(x => x.Name.Equals("jackpot")).FirstOrDefaultAsync();
                if (jackpotSetting != null)
                {
                    jackpot = jackpotSetting.LongSetting;
                }
            }
            return jackpot;
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