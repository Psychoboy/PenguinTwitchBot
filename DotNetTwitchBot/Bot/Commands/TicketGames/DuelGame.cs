using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models.Duel;

namespace DotNetTwitchBot.Bot.Commands.TicketGames
{
    public class DuelGame : BaseCommand
    {
        List<PendingDuel> PendingDuels { get; set; } = new List<PendingDuel>();
        static SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1);

        private ViewerFeature _viewerFeature;
        private TicketsFeature _ticketsFeature;

        public DuelGame(
            ServiceBackbone serviceBackbone,
            TicketsFeature ticketsFeature,
            ViewerFeature viewerFeature
            ) : base(serviceBackbone)
        {
            _viewerFeature = viewerFeature;
            _ticketsFeature = ticketsFeature;
        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            switch (e.Command)
            {
                case "duel":
                    var isCoolDownExpired = await IsCoolDownExpiredWithMessage(e.Name, e.DisplayName, e.Command);
                    if (isCoolDownExpired == false) return;
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
                await _serviceBackbone.SendChatMessage(e.DisplayName, "You don't have any pending duels.");
                return;
            }

            try
            {
                await _semaphoreSlim.WaitAsync();
                var existingDuel = PendingDuels.Where(x => x.Defender.Equals(e.Name)).FirstOrDefault();
                if (existingDuel == null)
                {
                    await _serviceBackbone.SendChatMessage(e.DisplayName, "You don't have any pending duels targeting you.");

                }
                else
                {
                    PendingDuels.Remove(existingDuel);
                    await _serviceBackbone.SendChatMessage(existingDuel.Attacker, $"{existingDuel.Defender} has denied your duel.");
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
                await _serviceBackbone.SendChatMessage(e.DisplayName, "You don't have any pending duels.");
                return;
            }
            PendingDuel? existingDuel;
            try
            {
                await _semaphoreSlim.WaitAsync();
                existingDuel = PendingDuels.Where(x => x.Defender.Equals(e.Name)).FirstOrDefault();
                if (existingDuel == null)
                {
                    await _serviceBackbone.SendChatMessage(e.DisplayName, "You don't have any pending duels targeting you.");
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

            if ((await _ticketsFeature.GetViewerTickets(existingDuel.Attacker)) < existingDuel.Amount)
            {
                await _serviceBackbone.SendChatMessage($"{existingDuel.Attacker} doesn't have enough tickets anymore.");
                return;
            }

            if ((await _ticketsFeature.GetViewerTickets(existingDuel.Defender)) < existingDuel.Amount)
            {
                await _serviceBackbone.SendChatMessage($"{existingDuel.Defender} doesn't have enough tickets anymore.");
                return;
            }

            var removedTicketsFromAttacker = await _ticketsFeature.RemoveTicketsFromViewer(existingDuel.Attacker, existingDuel.Amount);
            if (removedTicketsFromAttacker == false)
            {
                await _serviceBackbone.SendChatMessage($"{existingDuel.Attacker} doesn't have enough tickets anymore.");
                return;
            }

            var removedTicketsFromDefender = await _ticketsFeature.RemoveTicketsFromViewer(existingDuel.Defender, existingDuel.Amount);
            if (removedTicketsFromDefender == false)
            {
                await _serviceBackbone.SendChatMessage($"{existingDuel.Defender} doesn't have enough tickets anymore.");
                await _ticketsFeature.GiveTicketsToViewer(existingDuel.Attacker, existingDuel.Amount); //refund for attack since we removed them already
                return;
            }

            var winner = Tools.Next(0, 100);
            if (winner < 50)
            {
                await _ticketsFeature.GiveTicketsToViewer(existingDuel.Attacker, existingDuel.Amount * 2);
                await _serviceBackbone.SendChatMessage($"/me {existingDuel.Attacker} won the Duel vs {existingDuel.Defender} PogChamp {existingDuel.Attacker} won {existingDuel.Amount} tickets FeelsGoodMan");
            }
            else
            {
                await _ticketsFeature.GiveTicketsToViewer(existingDuel.Defender, existingDuel.Amount * 2);
                await _serviceBackbone.SendChatMessage($"/me {existingDuel.Defender} won the Duel vs {existingDuel.Attacker} PogChamp {existingDuel.Defender} won {existingDuel.Amount} tickets FeelsGoodMan");
            }
        }

        private async Task Duel(CommandEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.TargetUser))
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, "You need to specify a target.");
                return;
            }
            if (e.Args.Count < 2)
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, "To use duel, !duel target amount");
                return;
            }

            long amount = 0;
            if (long.TryParse(e.Args[1], out amount) == false)
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, "Please enter a proper amount. !duel target amount");
                return;
            }

            if (amount < 1 || amount > 100)
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, "To duel you must choose an amount between 1 and 100");
                return;
            }
            var attackerTickets = await _ticketsFeature.GetViewerTickets(e.Name);
            if (attackerTickets < amount)
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, "You don't have that much.");
                return;
            }
            var existingDuel = await GetExistingDuel(e.Name);
            if (existingDuel != null)
            {
                if (existingDuel.Attacker.Equals(e.Name, StringComparison.CurrentCultureIgnoreCase))
                {
                    await _serviceBackbone.SendChatMessage(e.DisplayName, "You are already attacking someone, wait for that one to timeout or the defender to !accept/!deny it");
                }
                else
                {
                    await _serviceBackbone.SendChatMessage(e.DisplayName, $"You already have a pending duel with {existingDuel.Attacker}, !accept or !deny it.");
                }
                return;
            }
            var defender = await _viewerFeature.GetViewer(e.TargetUser);
            if (defender == null)
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, "Could not find that viewer");
                return;
            }
            existingDuel = await GetExistingDuel(defender.Username);
            if (existingDuel != null)
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, $"{defender.DisplayName} has a pending duel already. Please wait for that duel to end or time out."); ;
                return;
            }

            var defenderTickets = await _ticketsFeature.GetViewerTickets(defender.Username);
            if (defenderTickets < amount)
            {
                await _serviceBackbone.SendChatMessage(defender.Username, "They don't have that many tickets.");
                return;
            }

            PendingDuel pendingDuel = new PendingDuel
            {
                Attacker = e.Name,
                Defender = defender.Username,
                Amount = amount
            };

            try
            {
                await _semaphoreSlim.WaitAsync();
                PendingDuels.Add(pendingDuel);
            }
            finally
            {
                _semaphoreSlim.Release();
            }
            AddCoolDown(e.Name, e.Command, DateTime.Now.AddMinutes(10));
            await _serviceBackbone.SendChatMessage(defender.DisplayName, $"{e.DisplayName} has challenged you to a duel for {amount} tickets. You have 2 minutes to !accept or !deny the duel.");
        }

        private async Task<bool> CheckIfAlreadyInDuel(string name)
        {
            try
            {
                await _semaphoreSlim.WaitAsync();
                var existingDuel = PendingDuels
                    .Where(x => x.Attacker.Equals(name, StringComparison.CurrentCultureIgnoreCase) ||
                    x.Defender.Equals(name, StringComparison.CurrentCultureIgnoreCase)
                    ).FirstOrDefault();
                if (existingDuel != null && existingDuel.ExpiresAt > DateTime.Now)
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
                    .Where(x => x.Attacker.Equals(name, StringComparison.CurrentCultureIgnoreCase) ||
                    x.Defender.Equals(name, StringComparison.CurrentCultureIgnoreCase)
                    ).FirstOrDefault();
                if (existingDuel != null && existingDuel.ExpiresAt > DateTime.Now)
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
    }
}