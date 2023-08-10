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
        private readonly ILogger<GiveawayFeature> _logger;
        private readonly TicketsFeature _ticketsFeature;
        private readonly ViewerFeature _viewerFeature;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<GiveawayHub> _hubContext;
        private readonly List<string> Tickets = new();
        private readonly List<GiveawayWinner> Winners = new();

        public GiveawayFeature(
            ILogger<GiveawayFeature> logger,
            ServiceBackbone serviceBackbone,
            TicketsFeature ticketsFeature,
            ViewerFeature viewerFeature,
            IHubContext<GiveawayHub> hubContext,
            IServiceScopeFactory scopeFactory,
            CommandHandler commandHandler
            ) : base(serviceBackbone, commandHandler)
        {
            _logger = logger;
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
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            switch (command.CommandProperties.CommandName)
            {
                case "enter":
                    {
                        if (e.Args.Count() == 0)
                        {
                            await ServiceBackbone.SendChatMessage(e.Name, "To enter tickets, please use !enter AMOUNT/MAX/ALL");
                            throw new SkipCooldownException();
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
                        await Draw();
                        break;
                    }
                case "close":
                    {
                        await Close();
                        break;
                    }
                case "resetdraw":
                    {
                        await Reset();
                        break;
                    }
                case "setprize":
                    {
                        await SetPrize(e.Arg);
                        break;
                    }
            }
        }

        public async Task<string> GetPrize()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
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

        public async Task SetPrize(string arg)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var prize = await db.Settings.Where(x => x.Name.Equals("GiveawayPrize")).FirstOrDefaultAsync();
                prize ??= new Setting()
                    {
                        Name = "GiveawayPrize"
                    };
                prize.StringSetting = arg;
                db.Update(prize);
                await db.SaveChangesAsync();
            }
            await _hubContext.Clients.All.SendAsync("Prize", arg);
        }

        public async Task Close()
        {
            Tickets.Clear();
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var entries = await db.GiveawayEntries.ToListAsync();
            foreach (var entry in entries)
            {
                Tickets.AddRange(Enumerable.Repeat(entry.Username, entry.Tickets));
            }
        }

        public async Task Reset()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.GiveawayEntries.ExecuteDeleteAsync();
            await db.SaveChangesAsync();
        }

        public async Task Draw()
        {
            if (Tickets.Count == 0)
            {
                await Close();
            }
            if (Tickets.Count == 0) return;
            var winningTicket = Tickets.RandomElement(_logger);
            var viewer = await _viewerFeature.GetViewer(winningTicket);
            var isFollower = await _viewerFeature.IsFollower(winningTicket);
            await ServiceBackbone.SendChatMessage(string.Format("{0} won the {1} and {2} following", viewer != null ? viewer.NameWithTitle() : winningTicket, await GetPrize(), isFollower ? "is" : "is not"));
            await AddWinner(viewer);
        }

        public async Task<List<GiveawayWinner>> PastWinners()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.GiveawayWinners.OrderByDescending(x => x.WinningDate).ToListAsync();
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
                await ServiceBackbone.SendChatMessage(string.Format("@{0}, please use a number or max/all when entering.", displayName));
                throw new SkipCooldownException();
            }
            if (points == 0 || points > viewerPoints)
            {
                await ServiceBackbone.SendChatMessage(string.Format("@{0}, you do not have enough or that many tickets to enter.", displayName));
                throw new SkipCooldownException();
            }

            if (points < 0)
            {
                await ServiceBackbone.SendChatMessage(string.Format("@{0}, don't be dumb.", displayName));
                throw new SkipCooldownException();
            }

            var enteredTickets = await GetEntriesCount(sender);
            if (points + enteredTickets > 1000000)
            {
                points = 1000000 - points;
                await ServiceBackbone.SendChatMessage(displayName, string.Format("Max entries is 1,000,000, so entering {0} instead to max you out.", points));
                if (points == 0)
                {
                    throw new SkipCooldownException();
                }
            }

            if (!(await _ticketsFeature.RemoveTicketsFromViewer(sender, points)))
            {
                await ServiceBackbone.SendChatMessage(displayName, "failed to enter giveaway. Please try again.");
                throw new SkipCooldownException();
            }

            using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var giveawayEntries = await db.GiveawayEntries.FirstOrDefaultAsync(x => x.Username.Equals(sender));
                giveawayEntries ??= new GiveawayEntry
                    {
                        Username = sender
                    };
                giveawayEntries.Tickets += points;
                db.GiveawayEntries.Update(giveawayEntries);
                await db.SaveChangesAsync();
            }
            await ServiceBackbone.SendChatMessage($"@{sender}, you have bought {points} entries.");
        }
        private async Task<long> GetEntriesCount(string sender)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var giveawayEntries = await db.GiveawayEntries.FirstOrDefaultAsync(x => x.Username.Equals(sender));
            giveawayEntries ??= new GiveawayEntry
                {
                    Username = sender
                };
            return giveawayEntries.Tickets;
        }

        private async Task Entries(string sender)
        {
            var entries = await GetEntriesCount(sender);
            await ServiceBackbone.SendWhisperMessage(sender, $"You have {entries} entries");
        }


    }
}