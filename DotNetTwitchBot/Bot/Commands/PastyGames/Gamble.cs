using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class Gamble : BaseCommand
    {
        private LoyaltyFeature _loyaltyFeature;
        private IServiceScopeFactory _scopeFactory;

        public Gamble(
            LoyaltyFeature loyaltyFeature,
            IServiceScopeFactory scopeFactory,
            ServiceBackbone serviceBackbone
            ) : base(serviceBackbone)
        {
            _loyaltyFeature = loyaltyFeature;
            _scopeFactory = scopeFactory;
        }

        public int JackPotNumber { get; } = 69;
        public long JackpotDefault { get; } = 1000;
        public int WinRange { get; } = 48;

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            switch (e.Command)
            {
                case "testgamble":
                    await HandleGamble(e);
                    break;
                case "testjackpot":
                    var jackpot = await GetJackpot();
                    await _serviceBackbone.SendChatMessage(e.DisplayName,
                    string.Format("The current jackpot is {0}", jackpot.ToString("N0")));
                    break;
            }
        }

        private async Task HandleGamble(CommandEventArgs e)
        {
            if (!IsCoolDownExpired(e.Name, e.Command)) return;
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
            var value = Tools.CurrentThreadRandom.Next(1, 100);
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
                await _serviceBackbone.SendChatMessage(string.Format("/announce {0} rolled {1} and won the jackpot of {2} pasties!", e.DisplayName, value, jackpotWinnings.ToString("N0")));
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
            AddCoolDown(e.Name, e.Command, 180);
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