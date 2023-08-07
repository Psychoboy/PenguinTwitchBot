using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Events;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class DailyCounter : BaseCommandService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DailyCounter> _logger;
        private static readonly string SettingName = "DailyCounterFormat";
        private static readonly string CountValue = "DailyCountValue";

        public DailyCounter(
            ILogger<DailyCounter> logger,
            ServiceBackbone serviceBackbone,
            IServiceScopeFactory scopeFactory,
            CommandHandler commandHandler
            ) : base(serviceBackbone, commandHandler)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            ServiceBackbone.SubscriptionEvent += OnSub;
            ServiceBackbone.SubscriptionGiftEvent += OnGiftSub;

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

        public override async Task Register()
        {
            var moduleName = "DailyCounter";
            await RegisterDefaultCommand("setdailycountertext", this, moduleName, Rank.Streamer);
            await RegisterDefaultCommand("setdailyvalue", this, moduleName, Rank.Streamer);
            _logger.LogInformation($"Registered commands for {moduleName}");
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            switch (command.CommandProperties.CommandName)
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
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var counterValue = await db.Settings.Where(x => x.Name.Equals(CountValue)).FirstOrDefaultAsync();
            if (counterValue == null)
            {
                return 0;
            }
            return counterValue.IntSetting;
        }


        private async Task SetCounterValue(int value)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var counterValue = await db.Settings.Where(x => x.Name.Equals(CountValue)).FirstOrDefaultAsync();
                counterValue ??= new Setting()
                    {
                        Name = CountValue
                    };
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
                setting ??= new Setting()
                    {
                        Name = SettingName
                    };
                setting.StringSetting = format;
                db.Settings.Update(setting);
                await db.SaveChangesAsync();
            }
            await UpdateCounter();
        }

        private async Task UpdateCounter()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var setting = await db.Settings.Where(x => x.Name.Equals(SettingName)).FirstOrDefaultAsync();
            if (setting == null)
            {
                return;
            }
            var count = await db.Settings.Where(x => x.Name.Equals(CountValue)).FirstOrDefaultAsync();
            count ??= new Setting()
                {
                    IntSetting = 0
                };
            var format = setting.StringSetting;
            var value = count.IntSetting;
            var text = format.Replace("{value}", value.ToString()).Replace("{nextgoal}", NextValue(value).ToString());
            await WriteCounterFile(text);
        }

        private int NextValue(int currentValue)
        {
            currentValue++;
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