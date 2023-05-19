using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class DailyCounter : BaseCommand
    {
        private IServiceScopeFactory _scopeFactory;
        private static string SettingName = "DailyCounterFormat";
        private static string CountValue = "DailyCountValue";

        public DailyCounter(
            ServiceBackbone serviceBackbone,
            IServiceScopeFactory scopeFactory
            ) : base(serviceBackbone)
        {
            _scopeFactory = scopeFactory;
            _serviceBackbone.SubscriptionEvent += OnSub;
            _serviceBackbone.SubscriptionGiftEvent += OnGiftSub;

        }

        private async Task OnGiftSub(object? sender, SubscriptionGiftEventArgs e)
        {
            await SetCounterValue(await GetCounterValue() + e.GiftAmount);
        }

        private async Task OnSub(object? sender, SubscriptionEventArgs e)
        {
            if (e.IsGift) return;
            await SetCounterValue(await GetCounterValue() + 1);
        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            // var command = "setcountertext";
            if (_serviceBackbone.IsBroadcasterOrBot(e.Name) == false) return;
            switch (e.Command)
            {
                case "setdailycountertext":
                    await UpdateCounterText(e.Arg);
                    break;

                case "setdailyvalue":
                    if (int.TryParse(e.Arg, out var value))
                    {
                        await SetCounterValue(value);
                    }
                    break;
            }
        }

        private async Task<int> GetCounterValue()
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var counterValue = await db.Settings.Where(x => x.Name.Equals(CountValue)).FirstOrDefaultAsync();
                if (counterValue == null)
                {
                    return 0;
                }
                return counterValue.IntSetting;
            }
        }


        private async Task SetCounterValue(int value)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var counterValue = await db.Settings.Where(x => x.Name.Equals(CountValue)).FirstOrDefaultAsync();
                if (counterValue == null)
                {
                    counterValue = new Setting()
                    {
                        Name = CountValue
                    };
                }
                counterValue.IntSetting = value;
                db.Settings.Update(counterValue);
                await db.SaveChangesAsync();
            }
            await UpdateCounter();
        }

        private async Task UpdateCounterText(string format)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var setting = await db.Settings.Where(x => x.Name.Equals(SettingName)).FirstOrDefaultAsync();
                if (setting == null)
                {
                    setting = new Setting()
                    {
                        Name = SettingName
                    };
                }
                setting.StringSetting = format;
                db.Settings.Update(setting);
                await db.SaveChangesAsync();
            }
            await UpdateCounter();
        }

        private async Task UpdateCounter()
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var setting = await db.Settings.Where(x => x.Name.Equals(SettingName)).FirstOrDefaultAsync();
                if (setting == null)
                {
                    return;
                }
                var count = await db.Settings.Where(x => x.Name.Equals(CountValue)).FirstOrDefaultAsync();
                if (count == null)
                {
                    count = new Setting()
                    {
                        IntSetting = 0
                    };
                }
                var format = setting.StringSetting;
                var value = count.IntSetting;
                var text = format.Replace("{value}", value.ToString()).Replace("{nextgoal}", NextValue(value).ToString());
                await WriteCounterFile(text);
            }
        }

        private int NextValue(int currentValue)
        {
            var nextGoal = (int)Math.Ceiling((double)currentValue / 10) * 10;
            if (nextGoal == 0)
            {
                return 10;
            }
            return nextGoal;
        }

        private async Task WriteCounterFile(string data)
        {
            if (!Directory.Exists("Data"))
            {
                Directory.CreateDirectory("Data");
            }
            await File.WriteAllTextAsync($"Data/DailyCounter.txt", data);
        }

    }
}