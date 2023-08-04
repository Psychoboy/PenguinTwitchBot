using System.Security.Cryptography;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Database;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models.Giveaway;
using EFCore.BulkExtensions;
using Microsoft.AspNetCore.SignalR;
using DotNetTwitchBot.Bot.Hubs;

namespace DotNetTwitchBot.Bot.Commands.Features
{
    public class GiveawayFeature : BaseCommandService
    {
        private ILogger<GiveawayFeature> _logger;
        // private GiveawayData _giveawayData;
        private TicketsFeature _ticketsFeature;
        private ViewerFeature _viewerFeature;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<GiveawayHub> _hubContext;
        private List<string> Tickets = new List<string>();
        private List<GiveawayWinner> Winners = new List<GiveawayWinner>();

        public GiveawayFeature(
            ILogger<GiveawayFeature> logger,
            ServiceBackbone serviceBackbone,
            TicketsFeature ticketsFeature,
            ViewerFeature viewerFeature,
            IHubContext<GiveawayHub> hubContext,
            IServiceScopeFactory scopeFactory,
            CommandHandler commandHandler
            ) : base(serviceBackbone, scopeFactory, commandHandler)
        {
            _logger = logger;
            //_giveawayData = giveawayData;
            _ticketsFeature = ticketsFeature;
            _viewerFeature = viewerFeature;
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
        }

        public override async Task Register()
        {
            var moduleName = "GiveawayFeature";
            await RegisterDefaultCommand("enter", this, moduleName);
            await RegisterDefaultCommand("entries", this, moduleName);
            await RegisterDefaultCommand("draw", this, moduleName, Rank.Streamer);
            await RegisterDefaultCommand("close", this, moduleName, Rank.Streamer);
            await RegisterDefaultCommand("resetdraw", this, moduleName, Rank.Streamer);
            await RegisterDefaultCommand("setprize", this, moduleName, Rank.Streamer);
            _logger.LogInformation($"Registered commands for {moduleName}");
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = _commandHandler.GetCommand(e.Command);
            if (command == null) return;
            switch (command.CommandProperties.CommandName)
            {
                case "enter":
                    {
                        if (e.Args.Count() == 0)
                        {
                            await _serviceBackbone.SendChatMessage(e.Name, "To enter tickets, please use !enter AMOUNT/MAX/ALL");
                            return;
                        }
                        await Enter(e.Name, e.Args.First());
                        break;
                    }
                case "entries":
                    {
                        await Entries(e.Name);
                        break;
                    }
                case "draw":
                    {
                        if (!e.isBroadcaster) return;
                        await Draw();
                        break;
                    }
                case "close":
                    {
                        if (!e.isBroadcaster) return;
                        await Close();
                        break;
                    }
                case "resetdraw":
                    {
                        if (!e.isBroadcaster) return;
                        await Reset();
                        break;
                    }
                case "setprize":
                    {
                        if (!e.isBroadcaster) return;
                        await SetPrize(e.Arg);
                        break;
                    }

            }
        }

        public async Task<string> GetPrize()
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var prize = await db.Settings.Where(x => x.Name.Equals("GiveawayPrize")).FirstOrDefaultAsync();
                if (prize == null)
                {
                    return "No Prize";
                }
                else
                {
                    return prize.StringSetting;
                }
            }
        }

        public async Task SetPrize(string arg)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var prize = await db.Settings.Where(x => x.Name.Equals("GiveawayPrize")).FirstOrDefaultAsync();
                if (prize == null)
                {
                    prize = new Setting()
                    {
                        Name = "GiveawayPrize"
                    };
                }
                prize.StringSetting = arg;
                db.Update(prize);
                await db.SaveChangesAsync();
            }
            await _hubContext.Clients.All.SendAsync("Prize", arg);
        }

        public async Task Close()
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

        public async Task Reset()
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await db.GiveawayEntries.ExecuteDeleteAsync();
                await db.SaveChangesAsync();
            }
        }

        public async Task Draw()
        {
            if (Tickets.Count == 0)
            {
                //await _serviceBackbone.SendChatMessage("Closing Giveaway prior to drawing ticket");
                await Close();
            }
            if (Tickets.Count == 0) return;
            var winningTicket = Tickets.RandomElement(_logger);
            var viewer = await _viewerFeature.GetViewer(winningTicket);
            var isFollower = await _viewerFeature.IsFollower(winningTicket);
            await _serviceBackbone.SendChatMessage(string.Format("{0} won the {1} and {2} following", viewer != null ? viewer.NameWithTitle() : winningTicket, await GetPrize(), isFollower ? "is" : "is not"));
            await AddWinner(viewer);
        }

        public async Task<List<GiveawayWinner>> PastWinners()
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                return await db.GiveawayWinners.OrderByDescending(x => x.WinningDate).ToListAsync();
            }
        }

        private async Task AddWinner(Viewer? viewer)
        {
            if (viewer == null) return;

            var prize = await GetPrize();
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var winner = new GiveawayWinner()
                {
                    Username = viewer.DisplayName,
                    Prize = prize
                };
                Winners.Add(winner);
                db.GiveawayWinners.Add(winner);
                await db.SaveChangesAsync();
            }
            await _hubContext.Clients.All.SendAsync("Winners", Winners);
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
            await _serviceBackbone.SendWhisperMessage(sender, $"You have {entries} entries");
            // await _serviceBackbone.SendChatMessage($"@{sender}, you have {entries} entries.");
        }


    }
}