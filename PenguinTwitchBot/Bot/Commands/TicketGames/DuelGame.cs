using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Models.Duel;

namespace PenguinTwitchBot.Bot.Commands.TicketGames
{
    public class DuelGame(
        IServiceBackbone serviceBackbone,
        IPointsSystem pointsSystem,
        IViewerFeature viewerFeature,
        ICommandHandler commandHandler,
        Application.Notifications.IPenguinDispatcher dispatcher,
        ILogger<DuelGame> logger,
        PenguinTwitchBot.Bot.Commands.Games.IGameSettingsService gameSettingsService
            ) : BaseCommandService(serviceBackbone, commandHandler, "DuelGame", dispatcher), IHostedService
    {
        List<PendingDuel> PendingDuels { get; set; } = [];
        static readonly SemaphoreSlim _semaphoreSlim = new(1);

        public override async Task Register()
        {
            var moduleName = "Duel";
            await RegisterDefaultCommand("duel", this, moduleName, Rank.Viewer, userCooldown: 600);
            await RegisterDefaultCommand("accept", this, moduleName, Rank.Viewer);
            await RegisterDefaultCommand("deny", this, moduleName, Rank.Viewer);
            await pointsSystem.RegisterDefaultPointForGame(ModuleName);
            logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            switch (command.CommandProperties.CommandName)
            {
                case "duel":
                    await Duel(e);
                    break;

                case "accept":
                    await AcceptDuel(e);
                    break;

                case "deny":
                    await DenyDuel(e);
                    break;
            }
        }

        private async Task DenyDuel(CommandEventArgs e)
        {
            if ((await CheckIfAlreadyInDuel(e.Name)) == false)
            {
                await ServiceBackbone.ResponseWithMessage(e, "You don't have any pending duels.");
                return;
            }

            try
            {
                await _semaphoreSlim.WaitAsync();
                var existingDuel = PendingDuels.Where(x => x.Defender.Equals(e.Name)).FirstOrDefault();
                if (existingDuel == null)
                {
                    await ServiceBackbone.ResponseWithMessage(e, "You don't have any pending duels targeting you.");

                }
                else
                {
                    PendingDuels.Remove(existingDuel);
                    await ServiceBackbone.SendChatMessage(existingDuel.Attacker, $"{existingDuel.Defender} has denied your duel.");
                }
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        private async Task AcceptDuel(CommandEventArgs e)
        {
            if ((await CheckIfAlreadyInDuel(e.Name)) == false)
            {
                await ServiceBackbone.ResponseWithMessage(e, "You don't have any pending duels.");
                return;
            }
            PendingDuel? existingDuel;
            try
            {
                await _semaphoreSlim.WaitAsync();
                existingDuel = PendingDuels.Where(x => x.Defender.Equals(e.Name)).FirstOrDefault();
                if (existingDuel == null)
                {
                    await ServiceBackbone.ResponseWithMessage(e, "You don't have any pending duels targeting you.");
                    return;
                }

                PendingDuels.Remove(existingDuel);
            }
            finally
            {
                _semaphoreSlim.Release();
            }

            await FightDuel(existingDuel);
        }

        private async Task FightDuel(PendingDuel existingDuel)
        {
            var pointType = await pointsSystem.GetPointTypeForGame(ModuleName);

            if ((await pointsSystem.GetUserPointsByUsernameAndGame(existingDuel.Attacker, ModuleName)).Points < existingDuel.Amount)
            {
                await ServiceBackbone.SendChatMessage($"{existingDuel.Attacker} doesn't have enough {pointType.Name} anymore.");
                return;
            }

            if ((await pointsSystem.GetUserPointsByUsernameAndGame(existingDuel.Defender, ModuleName)).Points < existingDuel.Amount)
            {
                await ServiceBackbone.SendChatMessage($"{existingDuel.Defender} doesn't have enough {pointType.Name} anymore.");
                return;
            }

            var removedTicketsFromAttacker = await pointsSystem.RemovePointsFromUserByUsernameAndGame(existingDuel.Attacker, ModuleName, existingDuel.Amount);
            if (removedTicketsFromAttacker == false)
            {
                await ServiceBackbone.SendChatMessage($"{existingDuel.Attacker} doesn't have enough {pointType.Name} anymore.");
                return;
            }

            var removedTicketsFromDefender = await pointsSystem.RemovePointsFromUserByUsernameAndGame(existingDuel.Defender, ModuleName, existingDuel.Amount);
            if (removedTicketsFromDefender == false)
            {
                await ServiceBackbone.SendChatMessage($"{existingDuel.Defender} doesn't have enough {pointType.Name} anymore.");
                await pointsSystem.AddPointsByUsernameAndGame(existingDuel.Attacker, ModuleName, existingDuel.Amount); //refund for attack since we removed them already
                return;
            }

            var winner = StaticTools.Next(0, 100);
            if (winner < existingDuel.WinChancePercent)
            {
                await pointsSystem.AddPointsByUsernameAndGame(existingDuel.Attacker, ModuleName, existingDuel.Amount * 2);
                await ServiceBackbone.SendChatMessage($"/me {existingDuel.Attacker} won the Duel vs {existingDuel.Defender} PogChamp {existingDuel.Attacker} won {existingDuel.Amount} {pointType.Name} FeelsGoodMan");
            }
            else
            {
                await pointsSystem.AddPointsByUsernameAndGame(existingDuel.Defender, ModuleName, existingDuel.Amount * 2);
                await ServiceBackbone.SendChatMessage($"/me {existingDuel.Defender} won the Duel vs {existingDuel.Attacker} PogChamp {existingDuel.Defender} won {existingDuel.Amount} {pointType.Name} FeelsGoodMan");
            }
        }

        private async Task Duel(CommandEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.TargetUser))
            {
                await ServiceBackbone.ResponseWithMessage(e, "You need to specify a target.");
                throw new SkipCooldownException();
            }
            if (e.Args.Count < 2)
            {
                await ServiceBackbone.ResponseWithMessage(e, "To use duel, !duel target amount");
                throw new SkipCooldownException();
            }

            if (long.TryParse(e.Args[1], out long amount) == false)
            {
                await ServiceBackbone.ResponseWithMessage(e, "Please enter a proper amount. !duel target amount");
                throw new SkipCooldownException();
            }

            var pointType = await pointsSystem.GetPointTypeForGame(ModuleName);
            var options = await DuelGameOptions.LoadAsync(pointType.Id, gameSettingsService);
            if (amount < options.MinimumWager || amount > options.MaximumWager)
            {
                await ServiceBackbone.ResponseWithMessage(e, $"To duel you must choose an amount between {options.MinimumWager:N0} and {options.MaximumWager:N0}");
                throw new SkipCooldownException();
            }
            var attackerTickets = await pointsSystem.GetUserPointsByUsernameAndGame(e.Name, ModuleName);
            if (attackerTickets.Points < amount)
            {
                await ServiceBackbone.ResponseWithMessage(e, "You don't have that much.");
                throw new SkipCooldownException();
            }
            var existingDuel = await GetExistingDuel(e.Name);
            if (existingDuel != null)
            {
                if (existingDuel.Attacker.Equals(e.Name, StringComparison.OrdinalIgnoreCase))
                {
                    await ServiceBackbone.ResponseWithMessage(e, "You are already attacking someone, wait for that one to timeout or the defender to !accept/!deny it");
                }
                else
                {
                    await ServiceBackbone.ResponseWithMessage(e, $"You already have a pending duel with {existingDuel.Attacker}, !accept or !deny it.");
                }
                throw new SkipCooldownException();
            }
            var defender = await viewerFeature.GetViewerByUserName(e.TargetUser);
            if (defender == null)
            {
                await ServiceBackbone.ResponseWithMessage(e, "Could not find that viewer");
                throw new SkipCooldownException();
            }
            existingDuel = await GetExistingDuel(defender.Username);
            if (existingDuel != null)
            {
                await ServiceBackbone.ResponseWithMessage(e, $"{defender.DisplayName} has a pending duel already. Please wait for that duel to end or time out.");
                throw new SkipCooldownException();
            }

            var defenderTickets = await pointsSystem.GetUserPointsByUsernameAndGame(defender.Username, ModuleName);
            if (defenderTickets.Points < amount)
            {
                await ServiceBackbone.ResponseWithMessage(e, $"They don't have that many {pointType.Name}.");
                throw new SkipCooldownException();
            }

            PendingDuel pendingDuel = new()
            {
                Attacker = e.Name,
                Defender = defender.Username,
                Amount = amount,
                ExpiresAt = DateTime.UtcNow.AddMinutes(options.ChallengeTimeoutMinutes),
                WinChancePercent = options.WinChancePercent
            };

            try
            {
                await _semaphoreSlim.WaitAsync(500);
                PendingDuels.Add(pendingDuel);
            }
            finally
            {
                _semaphoreSlim.Release();
            }
            var minuteLabel = options.ChallengeTimeoutMinutes == 1 ? "minute" : "minutes";
            await ServiceBackbone.SendChatMessage(defender.DisplayName, $"{e.DisplayName} has challenged you to a duel for {amount} {pointType.Name}. You have {options.ChallengeTimeoutMinutes} {minuteLabel} to !accept or !deny the duel.");
        }

        private async Task<bool> CheckIfAlreadyInDuel(string name)
        {
            try
            {
                await _semaphoreSlim.WaitAsync();
                var existingDuel = PendingDuels
                    .Where(x => x.Attacker.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                    x.Defender.Equals(name, StringComparison.OrdinalIgnoreCase)
                    ).FirstOrDefault();
                if (existingDuel != null && existingDuel.ExpiresAt > DateTime.UtcNow)
                {
                    return true;
                }
                if (existingDuel != null)
                {
                    PendingDuels.Remove(existingDuel);
                }
                return false;
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        private async Task<PendingDuel?> GetExistingDuel(string name)
        {
            try
            {
                await _semaphoreSlim.WaitAsync();
                var existingDuel = PendingDuels
                    .Where(x => x.Attacker.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                    x.Defender.Equals(name, StringComparison.OrdinalIgnoreCase)
                    ).FirstOrDefault();
                if (existingDuel != null && existingDuel.ExpiresAt > DateTime.UtcNow)
                {
                    return existingDuel;
                }
                if (existingDuel != null)
                {
                    PendingDuels.Remove(existingDuel);
                }
                return null;
            }
            finally
            {
                _semaphoreSlim.Release();
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
