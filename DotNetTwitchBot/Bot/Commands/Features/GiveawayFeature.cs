using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Hubs;
using DotNetTwitchBot.Bot.Models.Giveaway;
using DotNetTwitchBot.Bot.Repository;
using Microsoft.AspNetCore.SignalR;

namespace DotNetTwitchBot.Bot.Commands.Features
{
    public class GiveawayFeature : BaseCommandService
    {
        private readonly ILogger<GiveawayFeature> _logger;
        private readonly ITicketsFeature _ticketsFeature;
        private readonly IViewerFeature _viewerFeature;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<GiveawayHub> _hubContext;
        private readonly ILanguage lang;
        private readonly List<string> Tickets = new();
        private readonly List<GiveawayWinner> Winners = new();
        private readonly string PrizeSettingName = "GiveawayPrize";

        public GiveawayFeature(
            ILogger<GiveawayFeature> logger,
            IServiceBackbone serviceBackbone,
            ITicketsFeature ticketsFeature,
            IViewerFeature viewerFeature,
            IHubContext<GiveawayHub> hubContext,
            IServiceScopeFactory scopeFactory,
            ICommandHandler commandHandler,
            ILanguage language
            ) : base(serviceBackbone, commandHandler)
        {
            _logger = logger;
            _ticketsFeature = ticketsFeature;
            _viewerFeature = viewerFeature;
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
            lang = language;
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
            var command = CommandHandler.GetCommandDefaultName(e.Command);
            switch (command)
            {
                case "enter":
                    {
                        if (e.Args.Count() == 0)
                        {
                            await ServiceBackbone.SendChatMessage(e.Name, lang.Get("giveawayfeature.help.enter"));
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
            var prize = await GetCurrentPrize();
            if (prize == null)
            {
                return "No Prize";
            }
            else
            {
                return prize.StringSetting;
            }
        }

        private async Task<Setting?> GetCurrentPrize()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var prize = await db.Settings.Find(x => x.Name.Equals(PrizeSettingName)).FirstOrDefaultAsync();
            return prize;
        }

        public async Task SetPrize(string arg)
        {
            var prize = await GetCurrentPrize();
            prize ??= new Setting()
            {
                Name = PrizeSettingName
            };
            prize.StringSetting = arg;
            await AddOrUpdatePrize(prize);
            await _hubContext.Clients.All.SendAsync("Prize", arg);
        }

        private async Task AddOrUpdatePrize(Setting prize)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            if (prize.Id == null)
            {
                await db.Settings.AddAsync(prize);
            }
            else
            {
                db.Settings.Update(prize);
            }
            await db.SaveChangesAsync();
        }

        public List<string> ClosedTickets { get { return Tickets; } }

        public async Task Close()
        {
            Tickets.Clear();
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var entries = await db.GiveawayEntries.GetAllAsync();
            foreach (var entry in entries)
            {
                Tickets.AddRange(Enumerable.Repeat(entry.Username, entry.Tickets));
            }
        }

        public async Task Reset()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await db.GiveawayEntries.ExecuteDeleteAsync();
            await db.SaveChangesAsync();
            Tickets.Clear();
        }

        public async Task Draw()
        {
            if (Tickets.Count == 0)
            {
                await Close();
            }
            try
            {
                var winningTicket = Tickets.RandomElement(_logger);
                var viewer = await _viewerFeature.GetViewer(winningTicket);
                var isFollower = await _viewerFeature.IsFollower(winningTicket);

                await ServiceBackbone.SendChatMessage(lang.Get("giveawayfeature.draw.winner")
                    .Replace("(name)", viewer != null ? viewer.NameWithTitle() : winningTicket)
                    .Replace("(prize)", await GetPrize())
                    .Replace("(isFollowingCheck)", isFollower ? "is" : "is not")
                    );
                await AddWinner(viewer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error drawing a ticket.");
            }
        }

        public async Task<List<GiveawayWinner>> PastWinners()
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var winners = await db.GiveawayWinners.GetAllAsync();
                return winners.OrderByDescending(x => x.WinningDate).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting past winners");
                return new List<GiveawayWinner> { };
            }
        }

        private async Task AddWinner(Viewer? viewer)
        {
            if (viewer == null) return;

            var prize = await GetPrize();
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var winner = new GiveawayWinner()
                {
                    Username = viewer.DisplayName,
                    Prize = prize
                };
                Winners.Add(winner);
                await db.GiveawayWinners.AddAsync(winner);
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
                await ServiceBackbone.SendChatMessage(displayName, lang.Get("giveawayfeature.enter.notvalid"));
                throw new SkipCooldownException();
            }
            if (points == 0 || points > viewerPoints)
            {
                await ServiceBackbone.SendChatMessage(displayName, lang.Get("giveawayfeature.enter.notenough"));
                throw new SkipCooldownException();
            }

            if (points < 0)
            {
                await ServiceBackbone.SendChatMessage(displayName, lang.Get("giveawayfeature.enter.minus"));
                throw new SkipCooldownException();
            }

            var enteredTickets = await GetEntriesCount(sender);
            if (points + enteredTickets > 1000000)
            {
                points = 1000000 - points;
                await ServiceBackbone.SendChatMessage(displayName,
                    lang.Get("giveawayfeature.enter.max").Replace("(maxallowed)", "1,000,000").Replace("(amount)", points.ToString()));
                if (points == 0)
                {
                    throw new SkipCooldownException();
                }
            }

            if (!(await _ticketsFeature.RemoveTicketsFromViewer(sender, points)))
            {
                await ServiceBackbone.SendChatMessage(displayName, lang.Get("giveawayfeature.enter.failure"));
                throw new SkipCooldownException();
            }

            using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var giveawayEntries = await db.GiveawayEntries.Find(x => x.Username.Equals(sender)).FirstOrDefaultAsync();
                giveawayEntries ??= new GiveawayEntry
                {
                    Username = sender
                };
                giveawayEntries.Tickets += points;
                db.GiveawayEntries.Update(giveawayEntries);
                await db.SaveChangesAsync();
            }
            await ServiceBackbone.SendChatMessage(sender, lang.Get("giveawayfeature.enter.success").Replace("(amount)", points.ToString()));
        }
        private async Task<long> GetEntriesCount(string sender)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var giveawayEntries = await db.GiveawayEntries.Find(x => x.Username.Equals(sender)).FirstOrDefaultAsync();
            giveawayEntries ??= new GiveawayEntry
            {
                Username = sender
            };
            return giveawayEntries.Tickets;
        }

        private async Task Entries(string sender)
        {
            var entries = await GetEntriesCount(sender);
            await ServiceBackbone.SendWhisperMessage(sender, lang.Get("giveawayfeature.enter.entries").Replace("(amount)", entries.ToString()));
        }


    }
}