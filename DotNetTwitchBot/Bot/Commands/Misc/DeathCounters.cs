using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.TwitchServices;
using DotNetTwitchBot.Repository;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class DeathCounters : BaseCommandService, IHostedService
    {
        private readonly ITwitchService _twitchService;
        private readonly ILogger<DeathCounters> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IViewerFeature _viewerFeature;

        public DeathCounters(
            ITwitchService twitchService,
            ILogger<DeathCounters> logger,
            IServiceBackbone serviceBackbone,
            IViewerFeature viewerFeature,
            IServiceScopeFactory scopeFactory,
            ICommandHandler commandHandler
            ) : base(serviceBackbone, commandHandler, "DeathCounters")
        {
            _twitchService = twitchService;
            _logger = logger;
            _scopeFactory = scopeFactory;
            _viewerFeature = viewerFeature;
        }

        public override async Task Register()
        {
            var moduleName = "DeathCounter";
            await RegisterDefaultCommand("death", this, moduleName);
            _logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommandDefaultName(e.Command);
            if (!command.Equals("death")) return;

            var game = await _twitchService.GetCurrentGame();
            if (string.IsNullOrWhiteSpace(game))
            {
                _logger.LogWarning("Game is not set for counter");
                throw new SkipCooldownException();
            }
            var modifiers = e.Args;
            if (modifiers.Count > 0)
            {
                // TODO: Make modifiers customizable
                switch (modifiers.First())
                {
                    case "+":
                        if (e.IsBroadcaster || e.IsMod)
                        {
                            var counter = await GetCounter(game);
                            counter.Amount++;
                            await UpdateCounter(counter);

                        }
                        break;
                    case "-":
                        if (e.IsBroadcaster || e.IsMod)
                        {
                            var counter = await GetCounter(game);
                            counter.Amount--;
                            if (counter.Amount < 0) counter.Amount = 0;
                            await UpdateCounter(counter);
                        }
                        break;
                    case "reset":
                        if (e.IsBroadcaster || e.IsMod)
                        {
                            var counter = await GetCounter(game);
                            counter.Amount = 0;
                            await UpdateCounter(counter);
                        }
                        break;
                    case "set":
                        if (e.IsBroadcaster || e.IsMod)
                        {
                            if (modifiers.Count <= 1) throw new SkipCooldownException();
                            if (Int32.TryParse(modifiers[1], out var amount))
                            {
                                var counter = await GetCounter(game);
                                counter.Amount = amount;
                                await UpdateCounter(counter);
                            }
                        }
                        break;
                }
            }
            await SendCounter(game);
        }

        private async Task SendCounter(string counterName)
        {
            var counter = await GetCounter(counterName);
            if (counter.Amount == 0)
            {
                await SendChatMessage(string.Format("{0} has not died in {1} YET", await _viewerFeature.GetDisplayNameByUsername(ServiceBackbone.BroadcasterName), counterName));

            }
            else
            {
                await SendChatMessage(string.Format("{0} has died {1} times in {2}", await _viewerFeature.GetDisplayNameByUsername(ServiceBackbone.BroadcasterName), counter.Amount, counterName));
            }
        }

        private async Task<Models.DeathCounter> GetCounter(string counterName)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var counter = await db.DeathCounters.Find(x => x.Game.Equals(counterName)).FirstOrDefaultAsync();
            counter ??= new Models.DeathCounter
            {
                Game = counterName
            };
            return counter;
        }

        private async Task UpdateCounter(Models.DeathCounter counter)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.DeathCounters.Update(counter);
            await db.SaveChangesAsync();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting {moduledname}", ModuleName);
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopped {moduledname}", ModuleName);
            return Task.CompletedTask;
        }
    }
}