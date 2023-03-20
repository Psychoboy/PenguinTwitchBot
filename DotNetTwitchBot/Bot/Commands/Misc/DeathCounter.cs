using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class DeathCounter : BaseCommand
    {
        private TwitchService _twitchService;
        private ILogger<DeathCounter> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ViewerFeature _viewerFeature;

        public DeathCounter(
            TwitchService twitchService,
            ILogger<DeathCounter> logger,
            ServiceBackbone serviceBackbone,
            ViewerFeature viewerFeature,
            IServiceScopeFactory scopeFactory
            ) : base(serviceBackbone)
        {
            _twitchService = twitchService;
            _logger = logger;
            _scopeFactory = scopeFactory;
            _viewerFeature = viewerFeature;
        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            if (!e.Command.Equals("testdeath")) return;
            var game = await _twitchService.GetCurrentGame();
            if (string.IsNullOrWhiteSpace(game))
            {
                _logger.LogWarning("Game is not set for counter");
                return;
            }
            var modifiers = e.Args;
            if (modifiers.Count > 0 && _serviceBackbone.IsBroadcasterOrBot(e.Name))
            {
                switch (modifiers.First())
                {
                    case "+":
                        {
                            var counter = await GetCounter(game);
                            counter.Amount++;
                            await UpdateCounter(counter);
                            break;
                        }
                    case "-":
                        {
                            var counter = await GetCounter(game);
                            counter.Amount--;
                            if (counter.Amount < 0) counter.Amount = 0;
                            await UpdateCounter(counter);
                            break;
                        }
                    case "reset":
                        {
                            var counter = await GetCounter(game);
                            counter.Amount = 0;
                            await UpdateCounter(counter);
                            break;
                        }
                    case "set":
                        {
                            if (!(modifiers.Count > 1)) return;
                            if (Int32.TryParse(modifiers[1], out var amount))
                            {
                                var counter = await GetCounter(game);
                                counter.Amount = amount;
                                await UpdateCounter(counter);
                            }
                            break;
                        }
                }
            }
            await SendCounter(game);
        }

        private async Task SendCounter(string counterName)
        {
            var counter = await GetCounter(counterName);
            if (counter.Amount == 0)
            {
                await SendChatMessage(string.Format("{0} has not died in {1} YET", await _viewerFeature.GetDisplayName(_serviceBackbone.BroadcasterName), counterName));

            }
            else
            {
                await SendChatMessage(string.Format("{0} has died {1} times in {2}", await _viewerFeature.GetDisplayName(_serviceBackbone.BroadcasterName), counter.Amount, counterName));
            }
        }

        private async Task<Counter> GetCounter(string counterName)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var counter = await db.Counters.FirstOrDefaultAsync(x => x.CounterName.Equals(counterName));
                if (counter == null)
                {
                    counter = new Counter
                    {
                        CounterName = counterName
                    };
                }
                return counter;
            }
        }

        private async Task UpdateCounter(Counter counter)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Counters.Update(counter);
                await db.SaveChangesAsync();
            }
        }
    }
}