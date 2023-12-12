using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Hubs;
using DotNetTwitchBot.Bot.Models.Giveaway;
using DotNetTwitchBot.Bot.Repository;
using Microsoft.AspNetCore.SignalR;
using Timer = System.Timers.Timer;

namespace DotNetTwitchBot.Bot.Commands.Features
{
    public class GiveawayFeature : BaseCommandService
    {
        private readonly ILogger<GiveawayFeature> _logger;
        private readonly ITicketsFeature _ticketsFeature;
        private readonly IViewerFeature _viewerFeature;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<MainHub> _hubContext;
        private readonly ILanguage lang;
        private readonly List<string> Tickets = new();
        private readonly List<GiveawayWinner> Winners = new();
        private readonly string PrizeSettingName = "GiveawayPrize";
        private readonly string ImageSettingName = "GiveawayPrizeImage";
        private readonly string PrizeTierName = "GiveawayPrizeTier";
        static readonly SemaphoreSlim _semaphoreSlim = new(1);
        private readonly Timer _timer = new(TimeSpan.FromSeconds(5).TotalMilliseconds);
        private static readonly Prometheus.Gauge NumberOfTicketsEntered = Prometheus.Metrics.CreateGauge("number_of_tickets_entered", "Number of Tickets entered since last stream start", labelNames: new[] { "viewer" });
        private readonly IServiceBackbone _serviceBackBone;

        public GiveawayFeature(
            ILogger<GiveawayFeature> logger,
            IServiceBackbone serviceBackbone,
            ITicketsFeature ticketsFeature,
            IViewerFeature viewerFeature,
            IHubContext<MainHub> hubContext,
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
            _timer.Elapsed += SendCurrentEntriesToAll;
            _serviceBackBone = serviceBackbone;
            _serviceBackBone.StreamStarted += StreamStarted;
        }

        private Task StreamStarted(object? sender)
        {
            return Task.Run(() =>
            {
                var labels = NumberOfTicketsEntered.GetAllLabelValues();
                foreach (var label in labels)
                {
                    NumberOfTicketsEntered.RemoveLabelled(label);
                }
            });
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
            _timer.Start();
            _logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommandDefaultName(e.Command);
            switch (command)
            {
                case "enter":
                    {
                        if (e.Args.Count == 0)
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

        public async Task<int> GetEntrantsCount()
        {
            return (await GetEntries()).Count();
        }

        public async Task<int> GetEntriesCount()
        {
            var entries = await GetEntries();
            return entries.Sum(x => x.Tickets);
        }

        private async void SendCurrentEntriesToAll(object? sender, System.Timers.ElapsedEventArgs e)
        {
            await _hubContext.Clients.All.SendAsync("UpdateTickets", await GetEntriesCount(), await GetEntrantsCount());
        }

        private async Task<IEnumerable<GiveawayEntry>> GetEntries()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.GiveawayEntries.GetAllAsync();
        }

        public async Task<string> GetImageUrl()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var prize = await db.Settings.Find(x => x.Name.Equals(ImageSettingName)).FirstOrDefaultAsync();
            return prize != null ? prize.StringSetting : "";
        }

        private async Task<Setting?> GetCurrentPrize()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var prize = await db.Settings.Find(x => x.Name.Equals(PrizeSettingName)).FirstOrDefaultAsync();
            return prize;
        }
        private async Task<Setting?> GetCurrentPrizeImage()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var prize = await db.Settings.Find(x => x.Name.Equals(ImageSettingName)).FirstOrDefaultAsync();
            return prize;
        }

        private async Task<Setting?> GetCurrentPrizeTier()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var prize = await db.Settings.Find(x => x.Name.Equals(PrizeTierName)).FirstOrDefaultAsync();
            return prize;
        }

        public async Task<string> GetPrizeTier()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var prize = await db.Settings.Find(x => x.Name.Equals(PrizeTierName)).FirstOrDefaultAsync();
            return prize != null ? prize.StringSetting : "";
        }

        public async Task SetPrizeTier(string? arg)
        {
            var prize = await GetCurrentPrizeTier();
            prize ??= new Setting()
            {
                Name = PrizeTierName
            };
            prize.StringSetting = arg ?? "";
            await AddOrUpdatePrize(prize);
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

        public async Task SetImageUrl(string? arg)
        {
            var prize = await GetCurrentPrizeImage();
            prize ??= new Setting()
            {
                Name = ImageSettingName
            };
            prize.StringSetting = arg ?? "";
            await AddOrUpdatePrize(prize);
            await _hubContext.Clients.All.SendAsync("PrizeImage", arg);
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
            var entries = await GetEntries();
            var tickets = new List<string>();
            foreach (var entry in entries)
            {
                tickets.AddRange(Enumerable.Repeat(entry.Username, entry.Tickets));
            }
            Tickets.AddRange(tickets.OrderBy(_ => Guid.NewGuid()).ToList());
        }

        public async Task Reset()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await db.GiveawayEntries.ExecuteDeleteAllAsync();
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
                var prize = await GetPrize();
                var message = lang.Get("giveawayfeature.draw.winner")
                    .Replace("(name)", viewer != null ? viewer.NameWithTitle() : winningTicket, StringComparison.CurrentCultureIgnoreCase)
                    .Replace("(prize)", prize, StringComparison.CurrentCultureIgnoreCase)
                    .Replace("(isFollowingCheck)", isFollower ? "is" : "is not", StringComparison.CurrentCultureIgnoreCase)
                    ;
                await ServiceBackbone.SendChatMessage(message);
                await AddWinner(viewer, isFollower);
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

        private async Task AddWinner(Viewer? viewer, bool isFollower)
        {
            if (viewer == null) return;

            var prize = await GetPrize();
            var prizeTier = await GetPrizeTier();
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var winner = new GiveawayWinner()
                {
                    Username = viewer.DisplayName,
                    Prize = prize,
                    PrizeTier = prizeTier,
                    IsFollowing = isFollower
                };
                Winners.Add(winner);
                await db.GiveawayWinners.AddAsync(winner);
                await db.SaveChangesAsync();
            }
            await _hubContext.Clients.All.SendAsync("Winners", Winners);
        }
        private async Task Enter(string sender, string amount)
        {
            await Enter(sender, amount, false);
        }


        public async Task<string> Enter(string sender, string amount, bool fromUi)
        {
            try
            {
                if (await _semaphoreSlim.WaitAsync(500) == false)
                {
                    _logger.LogWarning("Lock expired while waiting...");
                }
                amount = amount.ToLower();
                var viewerPoints = await _ticketsFeature.GetViewerTickets(sender);
                if (amount == "max" || amount == "all")
                {
                    amount = (await _ticketsFeature.GetViewerTickets(sender)).ToString();
                }
                var displayName = await _viewerFeature.GetDisplayName(sender);
                if (!Int32.TryParse(amount, out var points))
                {
                    var message = lang.Get("giveawayfeature.enter.notvalid");
                    if (!fromUi) await ServiceBackbone.SendChatMessage(displayName, message);

                    throw new SkipCooldownException(message);
                }
                if (points == 0 || points > viewerPoints)
                {
                    var message = lang.Get("giveawayfeature.enter.notenough");
                    if (!fromUi) await ServiceBackbone.SendChatMessage(displayName, message);

                    throw new SkipCooldownException(message);
                }

                if (points < 0)
                {
                    var message = lang.Get("giveawayfeature.enter.minus");
                    await ServiceBackbone.SendChatMessage(displayName, message);
                    throw new SkipCooldownException(message);
                }

                var enteredTickets = await GetEntriesCount(sender);
                if (points + enteredTickets > 1000000)
                {
                    points = 1000000 - points;
                    var message = lang.Get("giveawayfeature.enter.max").Replace("(maxallowed)", "1,000,000").Replace("(amount)", points.ToString());
                    if (!fromUi)
                    {
                        await ServiceBackbone.SendChatMessage(displayName, message);
                    }

                    if (points == 0)
                    {
                        throw new SkipCooldownException("Unknown error, try via chat.");
                    }
                }

                if (!(await _ticketsFeature.RemoveTicketsFromViewer(sender, points)))
                {
                    var message = lang.Get("giveawayfeature.enter.failure");
                    if (!fromUi)
                    {
                        await ServiceBackbone.SendChatMessage(displayName, message);
                    }

                    throw new SkipCooldownException(message);
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
                NumberOfTicketsEntered.WithLabels(sender).Inc(points);
                {
                    var message = lang.Get("giveawayfeature.enter.success").Replace("(amount)", points.ToString());
                    if (!fromUi) await ServiceBackbone.SendChatMessage(sender, message);
                    return message;
                }
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }
        public async Task<long> GetEntriesCount(string sender)
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