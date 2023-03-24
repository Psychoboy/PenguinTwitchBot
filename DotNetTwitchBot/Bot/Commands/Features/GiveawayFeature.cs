using System.Security.Cryptography;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Database;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Models;
using EFCore.BulkExtensions;

namespace DotNetTwitchBot.Bot.Commands.Features
{
    public class GiveawayFeature : BaseCommand
    {
        private ILogger<GiveawayFeature> _logger;
        // private GiveawayData _giveawayData;
        private TicketsFeature _ticketsFeature;
        private ViewerFeature _viewerFeature;
        private readonly IServiceScopeFactory _scopeFactory;
        private List<string> Tickets = new List<string>();

        public GiveawayFeature(
            ILogger<GiveawayFeature> logger,
            ServiceBackbone eventService,
            TicketsFeature ticketsFeature,
            ViewerFeature viewerFeature,
            IServiceScopeFactory scopeFactory
            ) : base(eventService)
        {
            _logger = logger;
            //_giveawayData = giveawayData;
            _ticketsFeature = ticketsFeature;
            _viewerFeature = viewerFeature;
            _scopeFactory = scopeFactory;
        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            switch (e.Command)
            {
                case "giveme":
                    {
                        if (e.Args.Count() == 0) return;
                        await Enter(e.Name, e.Args.First());
                        break;
                    }
                case "entries":
                    {
                        await Entries(e.Name);
                        break;
                    }
                case "testdraw":
                    {
                        if (!e.isBroadcaster) return;
                        await Draw();
                        break;
                    }
                case "testclose":
                    {
                        if (!e.isBroadcaster) return;
                        await Close();
                        break;
                    }
                case "testresetdraw":
                    {
                        if (!e.isBroadcaster) return;
                        await Reset();
                        break;
                    }
                case "antares":
                    {
                        await _serviceBackbone.SendChatMessage(e.DisplayName, "Doing a special giveaway tonight for an antares! Do !special to see how many tickets you have then do !giveme # replacing # with the number of tickets or use all/max. Winner MUST be a follower and whisper me before next stream to claim.");
                        break;
                    }
            }
        }

        private async Task Close()
        {
            Tickets.Clear();
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var entries = await db.GiveawayEntries.ToListAsync();
                foreach (var entry in entries)
                {
                    Tickets.AddRange(Enumerable.Repeat(entry.Username, entry.Tickets));
                }
            }
        }

        private async Task Reset()
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await db.GiveawayEntries.ExecuteDeleteAsync();
                await db.SaveChangesAsync();
            }
        }

        private async Task Draw()
        {
            if (Tickets.Count == 0)
            {
                await _serviceBackbone.SendChatMessage("Closing Giveaway prior to drawing ticket");
                await Close();
            }
            if (Tickets.Count == 0) return;
            var winningTicket = Tickets[Tools.CurrentThreadRandom.Next(Tickets.Count)];
            var viewer = await _viewerFeature.GetViewer(winningTicket);
            var isFollower = await _viewerFeature.IsFollower(winningTicket);
            await _serviceBackbone.SendChatMessage(string.Format("{0} won the drawing and {1} following", viewer != null ? viewer.DisplayName : winningTicket, isFollower ? "is" : "is not"));
        }



        private async Task Enter(string sender, string amount)
        {
            amount = amount.ToLower();
            var viewerPoints = await _ticketsFeature.GetViewerTickets(sender);
            if (amount == "max" || amount == "all")
            {
                amount = (await _ticketsFeature.GetViewerTickets(sender)).ToString();
            }
            var displayName = await _viewerFeature.GetDisplayName(sender);
            if (!Int32.TryParse(amount, out var points))
            {
                await _serviceBackbone.SendChatMessage(string.Format("@{0}, please use a number or max/all when entering.", displayName));
                return;
            }
            if (points == 0 || points > viewerPoints)
            {
                await _serviceBackbone.SendChatMessage(string.Format("@{0}, you do not have enough or that many tickets to enter.", displayName));
                return;
            }

            if (points < 0)
            {
                await _serviceBackbone.SendChatMessage(string.Format("@{0}, don't be dumb.", displayName));
                return;
            }

            var enteredTickets = await GetEntriesCount(sender);
            if (points + enteredTickets > 1000000)
            {
                points = 1000000 - points;
                await _serviceBackbone.SendChatMessage(displayName, string.Format("Max entries is 1,000,000, so entering {0} instead to max you out.", points));
                if (points == 0)
                {
                    return;
                }
            }

            if (!(await _ticketsFeature.RemoveTicketsFromViewer(sender, points)))
            {
                await _serviceBackbone.SendChatMessage(displayName, "failed to enter giveaway. Please try again.");
                return;
            }

            using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var giveawayEntries = await db.GiveawayEntries.FirstOrDefaultAsync(x => x.Username.Equals(sender));
                if (giveawayEntries == null)
                {
                    giveawayEntries = new GiveawayEntry
                    {
                        Username = sender
                    };
                }
                giveawayEntries.Tickets += points;
                db.GiveawayEntries.Update(giveawayEntries);
                await db.SaveChangesAsync();
            }
            await _serviceBackbone.SendChatMessage($"@{sender}, you have bought {points} entries.");
        }
        private async Task<long> GetEntriesCount(string sender)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var giveawayEntries = await db.GiveawayEntries.FirstOrDefaultAsync(x => x.Username.Equals(sender));
                if (giveawayEntries == null)
                {
                    giveawayEntries = new GiveawayEntry
                    {
                        Username = sender
                    };
                }
                return giveawayEntries.Tickets;
            }
        }

        private async Task Entries(string sender)
        {
            var entries = await GetEntriesCount(sender);
            await _serviceBackbone.SendChatMessage($"@{sender}, you have {entries} entries.");
        }



    }
}