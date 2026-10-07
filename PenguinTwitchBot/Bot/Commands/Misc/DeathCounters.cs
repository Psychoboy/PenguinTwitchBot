using Microsoft.AspNetCore.SignalR;
using PenguinTwitchBot.Bot.Hubs;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Bot.Commands.Misc
{
    public class DeathCounters(
        ITwitchService twitchService,
        ILogger<DeathCounters> logger,
        IServiceBackbone serviceBackbone,
        IViewerFeature viewerFeature,
        IServiceScopeFactory scopeFactory,
        Application.Notifications.IPenguinDispatcher dispatcher,
        ICommandHandler commandHandler,
        IDefaultCommandTriggerService defaultCommandTriggerService,
        IHubContext<MainHub> hubContext
            ) : BaseCommandService(serviceBackbone, commandHandler, "DeathCounters", dispatcher), IHostedService, IDeathCounterService
    {
        public override async Task Register()
        {
            var moduleName = "DeathCounter";
            await RegisterDefaultCommand("death", this, moduleName);
            logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            if (!command.CommandProperties.CommandName.Equals("death")) return;

            var game = await twitchService.GetCurrentGame();
            if (string.IsNullOrWhiteSpace(game))
            {
                logger.LogWarning("Game is not set for counter");
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
                            var oldAmount = counter.Amount;
                            counter.Amount++;
                            await UpdateCounter(counter);

                            // Trigger death incremented event
                            await defaultCommandTriggerService.TriggerDefaultCommandEventAsync(
                                "death",
                                DefaultCommandEventTypes.DeathIncremented,
                                e,
                                new Dictionary<string, string>
                                {
                                    { "Game", game },
                                    { "NewCount", counter.Amount.ToString() },
                                    { "OldCount", oldAmount.ToString() }
                                });
                        }
                        break;
                    case "-":
                        if (e.IsBroadcaster || e.IsMod)
                        {
                            var counter = await GetCounter(game);
                            var oldAmount = counter.Amount;
                            counter.Amount--;
                            if (counter.Amount < 0) counter.Amount = 0;
                            await UpdateCounter(counter);

                            // Trigger death decremented event
                            await defaultCommandTriggerService.TriggerDefaultCommandEventAsync(
                                "death",
                                DefaultCommandEventTypes.DeathDecremented,
                                e,
                                new Dictionary<string, string>
                                {
                                    { "Game", game },
                                    { "NewCount", counter.Amount.ToString() },
                                    { "OldCount", oldAmount.ToString() }
                                });
                        }
                        break;
                    case "reset":
                        if (e.IsBroadcaster || e.IsMod)
                        {
                            var counter = await GetCounter(game);
                            var oldAmount = counter.Amount;
                            counter.Amount = 0;
                            await UpdateCounter(counter);

                            // Trigger death reset event
                            await defaultCommandTriggerService.TriggerDefaultCommandEventAsync(
                                "death",
                                DefaultCommandEventTypes.DeathReset,
                                e,
                                new Dictionary<string, string>
                                {
                                    { "Game", game },
                                    { "OldCount", oldAmount.ToString() }
                                });
                        }
                        break;
                    case "set":
                        if (e.IsBroadcaster || e.IsMod)
                        {
                            if (modifiers.Count <= 1) throw new SkipCooldownException();
                            if (Int32.TryParse(modifiers[1], out var amount))
                            {
                                var counter = await GetCounter(game);
                                var oldAmount = counter.Amount;
                                counter.Amount = amount;
                                await UpdateCounter(counter);

                                // Trigger death set event
                                await defaultCommandTriggerService.TriggerDefaultCommandEventAsync(
                                    "death",
                                    DefaultCommandEventTypes.DeathSet,
                                    e,
                                    new Dictionary<string, string>
                                    {
                                        { "Game", game },
                                        { "NewCount", amount.ToString() },
                                        { "OldCount", oldAmount.ToString() }
                                    });
                            }
                        }
                        break;
                }
            }
            await SendCounter(game, command.CommandProperties.SourceOnly);
        }

        private async Task SendCounter(string counterName, bool sourceOnly)
        {
            var counter = await GetCounter(counterName);
            if (counter.Amount == 0)
            {
                await SendChatMessage(string.Format("{0} has not died in {1} YET", await viewerFeature.GetDisplayNameByUsername(ServiceBackbone.BroadcasterName), counterName), sourceOnly);

            }
            else
            {
                await SendChatMessage(string.Format("{0} has died {1} times in {2}", await viewerFeature.GetDisplayNameByUsername(ServiceBackbone.BroadcasterName), counter.Amount, counterName), sourceOnly);
            }
        }

        private async Task<DeathCounter> GetCounter(string counterName)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var counter = await db.DeathCounters.Find(x => x.Game.Equals(counterName)).FirstOrDefaultAsync();
            counter ??= new DeathCounter
            {
                Game = counterName
            };
            return counter;
        }

        private async Task UpdateCounter(DeathCounter counter)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.DeathCounters.Update(counter);
            await db.SaveChangesAsync();

            await WriteCounterFile(counter.Game, counter.Amount);
            try
            {
                await hubContext.Clients.All.SendAsync("DeathCounterUpdated", new
                {
                    game = counter.Game,
                    amount = counter.Amount
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to broadcast DeathCounterUpdated for {Game}", counter.Game);
            }
        }

        public async Task<(string Game, int Amount)> GetCurrentDeathCountAsync()
        {
            var game = await twitchService.GetCurrentGame();
            if (string.IsNullOrWhiteSpace(game)) game = "Unknown";
            var counter = await GetCounter(game);
            return (game, counter.Amount);
        }

        public async Task<(string Game, int Amount)> AdjustCurrentDeathCountAsync(CounterOperation operation, int? value = null)
        {
            var game = await twitchService.GetCurrentGame();
            if (string.IsNullOrWhiteSpace(game)) game = "Unknown";
            var counter = await GetCounter(game);
            var oldAmount = counter.Amount;

            switch (operation)
            {
                case CounterOperation.Increment:
                    counter.Amount += (value ?? 1);
                    break;
                case CounterOperation.Decrement:
                    counter.Amount -= (value ?? 1);
                    if (counter.Amount < 0) counter.Amount = 0;
                    break;
                case CounterOperation.Reset:
                    counter.Amount = 0;
                    break;
                case CounterOperation.Set:
                    counter.Amount = Math.Max(0, value ?? 0);
                    break;
            }

            await UpdateCounter(counter);
            return (game, counter.Amount);
        }

        private static async Task WriteCounterFile(string game, int amount)
        {
            try
            {
                var dir = Path.Combine(Directory.GetCurrentDirectory(), "Data", "counters");
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                await File.WriteAllTextAsync(Path.Combine(dir, "death.txt"), amount.ToString());
                await File.WriteAllTextAsync(Path.Combine(dir, "death-full.txt"), $"Deaths ({game}): {amount}");
            }
            catch
            {
                // Best effort
            }
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
