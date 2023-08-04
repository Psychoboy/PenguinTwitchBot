using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.TwitchServices;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class Gamble : BaseCommandService
    {
        private LoyaltyFeature _loyaltyFeature;
        private IServiceScopeFactory _scopeFactory;
        private TwitchService _twitchServices;
        private readonly ILogger<Gamble> _logger;

        public Gamble(
            ILogger<Gamble> logger,
            LoyaltyFeature loyaltyFeature,
            IServiceScopeFactory scopeFactory,
            TwitchServices.TwitchService twitchServices,
            ServiceBackbone serviceBackbone,
            CommandHandler commandHandler
            ) : base(serviceBackbone, scopeFactory, commandHandler)
        {
            _loyaltyFeature = loyaltyFeature;
            _scopeFactory = scopeFactory;
            _twitchServices = twitchServices;
            _logger = logger;
        }

        public int JackPotNumber { get; } = 69;
        public long JackpotDefault { get; } = 1000;
        public int WinRange { get; } = 48;

        public override async Task Register()
        {
            var moduleName = "Gamble";
            await RegisterDefaultCommand("gamble", this, moduleName, Rank.Viewer);
            await RegisterDefaultCommand("jackpot", this, moduleName, Rank.Viewer);
            _logger.LogInformation($"Registered commands for {moduleName}");
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = _commandHandler.GetCommand(e.Command);
            if (command == null) return;
            switch (command.CommandProperties.CommandName)
            {
                case "gamble":
                    await HandleGamble(e);
                    break;
                case "jackpot":
                    var jackpot = await GetJackpot();
                    await _serviceBackbone.SendChatMessage(e.DisplayName,
                    string.Format("The current jackpot is {0}", jackpot.ToString("N0")));
                    break;
            }
        }

        private async Task HandleGamble(CommandEventArgs e)
        {
            var isCoolDownExpired = await IsCoolDownExpiredWithMessage(e.Name, e.DisplayName, e.Command);
            if (isCoolDownExpired == false) return;

            if (e.Args.Count == 0)
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName,
                "To gamble, do !gamble amount to specify amount or do !gamble max or all to do the max bet");
                return;
            }

            var amountStr = e.Args.First();
            var amount = 0L;
            if (amountStr.Equals("all", StringComparison.CurrentCultureIgnoreCase) ||
               amountStr.Equals("max", StringComparison.CurrentCultureIgnoreCase))
            {
                amount = await _loyaltyFeature.GetMaxPointsFromUser(e.Name);
            }
            else if (!Int64.TryParse(amountStr, out amount))
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName,
                "To gamble, do !gamble amount to specify amount or do !gamble max or all to do the max bet");
                return;
            }

            if (amount > LoyaltyFeature.MaxBet || amount < 5)
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, string.Format("The max bet is {0} and must be greater then 5", LoyaltyFeature.MaxBet.ToString("N0")));
                return;
            }

            var jackpot = await GetJackpot();

            await _loyaltyFeature.RemovePointsFromUser(e.Name, amount);
            var value = Tools.Next(1, 100 + 1);
            if (value == JackPotNumber)
            {
                var jackpotWinnings = jackpot * (amount / LoyaltyFeature.MaxBet);
                var winnings = amount * 2;
                jackpot = jackpot - jackpotWinnings;
                if (jackpot < JackpotDefault)
                {
                    jackpot = JackpotDefault;
                }
                await updateJackpot(jackpot, true);
                await _twitchServices.Announcement(string.Format("{0} rolled {1} and won the jackpot of {2} pasties!", e.DisplayName, value, jackpotWinnings.ToString("N0")));
                await LaunchFireworks();
                await _loyaltyFeature.AddPointsToViewer(e.Name, winnings + jackpotWinnings);
            }
            else if (value > WinRange)
            {
                var winnings = amount * 2;
                await _serviceBackbone.SendChatMessage(string.Format("{0} rolled {1} and won the {2} pasties!", e.DisplayName, value, winnings.ToString("N0")));
                await _loyaltyFeature.AddPointsToViewer(e.Name, winnings);
            }
            else
            {
                await updateJackpot(Convert.ToInt64(amount * 0.10), false);
                await _serviceBackbone.SendChatMessage(string.Format("{0} rolled {1} and lost {2} pasties", e.DisplayName, value, amount.ToString("N0")));
            }
            AddCoolDown(e.Name, e.Command, DateTime.Now.AddMinutes(3));
        }

        private async Task LaunchFireworks()
        {
            try
            {
                var httpClient = new HttpClient();
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri("http://127.0.0.1:7474/DoAction"),
                    Method = HttpMethod.Post,
                    Content = new StringContent("{\"action\":{\"id\":\"c4a5e3b8-a607-4b34-b8fe-ff7b36c3f3d4\",\"name\":\"Fireworks - General - 50 fireworks\"},\"args\": {}}")
                };
                var result = await httpClient.SendAsync(request);
            }
            catch (Exception)
            {
                // do nothing
            }
        }

        private async Task updateJackpot(long amount, bool reset)
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
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var jackpotSetting = await db.Settings.FirstOrDefaultAsync(x => x.Name.Equals("jackpot"));
                if (jackpotSetting == null)
                {
                    jackpotSetting = new Setting { Name = "jackpot", LongSetting = 0, DataType = Setting.DataTypeEnum.Long };
                }
                jackpotSetting.LongSetting = jackpot;
                db.Settings.Update(jackpotSetting);
                await db.SaveChangesAsync();
            }
        }

        private async Task<Int64> GetJackpot()
        {
            var jackpot = JackpotDefault;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var jackpotSetting = await db.Settings.FirstOrDefaultAsync(x => x.Name.Equals("jackpot"));
                if (jackpotSetting != null)
                {
                    jackpot = jackpotSetting.LongSetting;
                }
            }
            return jackpot;
        }


    }
}