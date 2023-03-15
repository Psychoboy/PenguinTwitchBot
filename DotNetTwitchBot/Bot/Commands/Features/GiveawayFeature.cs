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

        private async Task Reset()
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await db.GiveawayEntries.ExecuteDeleteAsync();
                await db.SaveChangesAsync();
            }

            // await _giveawayData
        }

        private async Task Draw()
        {
            GiveawayEntry? entry = null;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var count = await db.GiveawayEntries.CountAsync();
                var randomIndex = Tools.CurrentThreadRandom.Next(count);
                entry = await db.GiveawayEntries.OrderBy(p => Guid.NewGuid()).Skip(randomIndex).Take(1).FirstOrDefaultAsync();
            }
            if (entry == null) return;
            _logger.LogInformation("Entry Id: {0} Username {1} selected", entry.Id, entry.Username);
            var isFollower = await _viewerFeature.IsFollower(entry.Username);
            var viewer = await _viewerFeature.GetViewer(entry.Username);
            await _eventService.SendChatMessage(string.Format("{0} won the drawing and {1} following", viewer != null ? viewer.DisplayName : entry.Username, isFollower ? "is" : "is not"));

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
            if (!Int64.TryParse(amount, out var points))
            {
                await _eventService.SendChatMessage(string.Format("@{0}, please use a number or max/all when entering.", displayName));
                return;
            }
            if (points == 0 || points > viewerPoints)
            {
                await _eventService.SendChatMessage(string.Format("@{0}, you do not have enough or that many tickets to enter.", displayName));
                return;
            }

            if (points < 0) { await _eventService.SendChatMessage(string.Format("@{0}, don't be dumb.", displayName)); }

            if (!(await _ticketsFeature.RemoveTicketsFromViewer(sender, points)))
            {
                await _eventService.SendChatMessage(displayName, "failed to enter giveaway. Please try again.");
                return;
            }

            if (points > 1000000)
            {
                await _eventService.SendChatMessage("@{0}, Max entries is 1,000,000");
            }

            var entries = new GiveawayEntry[points];
            for (var i = 0; i < points; i++)
            {
                entries[i] = new GiveawayEntry()
                {
                    Username = sender
                };
            }

            using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                try
                {
                    db.BulkInsert(entries);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error bulk updating");
                }
            }
            await _eventService.SendChatMessage($"@{sender}, you have bought {points} entries.");
        }

        private async Task Entries(string sender)
        {

            var entries = 0;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                entries = await db.GiveawayEntries.Where(x => x.Username.Equals(sender)).CountAsync();
            }
            await _eventService.SendChatMessage($"@{sender}, you have {entries} entries.");
        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            switch (e.Command)
            {
                case "testenter":
                    {
                        if (e.Args.Count() == 0) return;
                        await Enter(e.Name, e.Args.First());
                        break;
                    }
                case "testentries":
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
                case "testresetdraw":
                    {
                        if (!e.isBroadcaster) return;
                        await Reset();
                        break;
                    }
                case "testprize":
                    {
                        break;
                    }
            }
        }

    }
}