using DotNetTwitchBot.Bot.Commands.Games;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Hubs;
using DotNetTwitchBot.Bot.Models.Giveaway;
using DotNetTwitchBot.Extensions;
using DotNetTwitchBot.Repository;
using Microsoft.AspNetCore.SignalR;
using System.Text;
using Timer = System.Timers.Timer;

namespace DotNetTwitchBot.Bot.Commands.Features
{
    public class GiveawayFeature(
        ILogger<GiveawayFeature> logger,
        IServiceBackbone serviceBackbone,
        IPointsSystem pointsSystem,
        IViewerFeature viewerFeature,
        IHubContext<MainHub> hubContext,
        IServiceScopeFactory scopeFactory,
        ICommandHandler commandHandler,
        IGameSettingsService gameSettingsService
            ) : BaseCommandService(serviceBackbone, commandHandler, "GiveawayFeature"), IHostedService
    {
        private readonly List<string> Tickets = new();
        private readonly List<GiveawayWinner> Winners = new();
        private readonly string PrizeSettingName = "GiveawayPrize";
        private readonly string ImageSettingName = "GiveawayPrizeImage";
        private readonly string PrizeTierName = "GiveawayPrizeTier";
        private readonly string GiveawayAdditionalDetailsSettingName = "GiveawayAdditionalDetails";
        static readonly SemaphoreSlim _semaphoreSlim = new(1);
        private readonly Timer _timer = new(TimeSpan.FromSeconds(5).TotalMilliseconds);
        private static readonly Prometheus.Gauge NumberOfTicketsEntered = Prometheus.Metrics.CreateGauge("number_of_tickets_entered", "Number of Tickets entered since last stream start", labelNames: new[] { "viewer" });

        private Task StreamStarted(object? sender, EventArgs _)
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
            await RegisterDefaultCommand("draw", this, moduleName, Rank.Streamer);
            await RegisterDefaultCommand("close", this, moduleName, Rank.Streamer);
            await RegisterDefaultCommand("resetdraw", this, moduleName, Rank.Streamer);
            await RegisterDefaultCommand("setprize", this, moduleName, Rank.Streamer);
            await pointsSystem.RegisterDefaultPointForGame(ModuleName);
            _timer.Start();
            logger.LogInformation("Registered commands for {moduleName}", moduleName);
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
                            await ServiceBackbone.SendChatMessage(e.Name, await gameSettingsService.GetStringSetting(ModuleName, "help.enter", "To enter tickets, please use !enter AMOUNT/MAX/ALL"));
                            throw new SkipCooldownException();
                        }
                        await Enter(e.Name, e.Args.First());
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
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.GiveawayEntries.CountAsync();
        }

        public async Task<int> GetEntriesCount()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.GiveawayEntries.GetSum();
        }

        public async Task<IEnumerable<GiveawayExclusion>> GetAllExclusions()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.GiveawayExclusions.GetAllAsync();
        }

        public async Task AddExclusion(GiveawayExclusion exclusion)
        {
            if (exclusion == null) return;
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await db.GiveawayExclusions.AddAsync(exclusion);
            await db.SaveChangesAsync();
        }

        public async Task DeleteExclusion(GiveawayExclusion exclusion)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.GiveawayExclusions.Remove(exclusion);
            await db.SaveChangesAsync();
        }

        private async void SendCurrentEntriesToAll(object? sender, System.Timers.ElapsedEventArgs e)
        {
            await hubContext.Clients.All.SendAsync("UpdateTickets", await GetEntriesCount(), await GetEntrantsCount());
        }

        private async Task<IEnumerable<GiveawayEntry>> GetEntries()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.GiveawayEntries.GetAllAsync();
        }

        private async Task<IEnumerable<GiveawayExclusion>> GetExclusions()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.GiveawayExclusions.Find(x => x.ExpireDateTime == null || x.ExpireDateTime > DateTime.Now).ToListAsync();
        }

        public async Task<string> GetImageUrl()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var prize = await db.Settings.Find(x => x.Name.Equals(ImageSettingName)).FirstOrDefaultAsync();
            return prize != null ? prize.StringSetting : "";
        }

        private async Task<Setting?> GetCurrentPrize()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var prize = await db.Settings.Find(x => x.Name.Equals(PrizeSettingName)).FirstOrDefaultAsync();
            return prize;
        }
        private async Task<Setting?> GetCurrentPrizeImage()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var prize = await db.Settings.Find(x => x.Name.Equals(ImageSettingName)).FirstOrDefaultAsync();
            return prize;
        }

        private async Task<Setting?> GetCurrentPrizeTier()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var prize = await db.Settings.Find(x => x.Name.Equals(PrizeTierName)).FirstOrDefaultAsync();
            return prize;
        }

        private async Task<Setting?> GetCurrentPrizeAdditionalDetails()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var prize = await db.Settings.Find(x => x.Name.Equals(GiveawayAdditionalDetailsSettingName)).FirstOrDefaultAsync();
            return prize;
        }

        public async Task<string> GetPrizeTier()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var prize = await db.Settings.Find(x => x.Name.Equals(PrizeTierName)).FirstOrDefaultAsync();
            return prize != null ? prize.StringSetting : "";
        }

        public async Task<string> GetPrizeAdditionalDetails()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var prize = await db.Settings.Find(x => x.Name.Equals(GiveawayAdditionalDetailsSettingName)).FirstOrDefaultAsync();
            return prize != null ? prize.StringSetting : "";
        }

        public async Task SetPrizeAdditionalDetails(string? arg)
        {
            var prize = await GetCurrentPrizeAdditionalDetails();
            prize ??= new Setting()
            {
                Name = GiveawayAdditionalDetailsSettingName
            };
            prize.StringSetting = arg ?? "";
            await AddOrUpdatePrize(prize);
            await hubContext.Clients.All.SendAsync("PrizeAdditionalDetails", arg);
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
            await hubContext.Clients.All.SendAsync("Prize", arg);
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
            await hubContext.Clients.All.SendAsync("PrizeImage", arg);
        }

        private async Task AddOrUpdatePrize(Setting prize)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
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
            var exclusions = await GetExclusions();
            foreach (var entry in entries)
            {
                if (exclusions.Where(x => x.Username.Equals(entry.Username, StringComparison.OrdinalIgnoreCase)).Any()) continue;
                tickets.AddRange(Enumerable.Repeat(entry.Username, entry.Tickets));
            }
            Tickets.AddRange(tickets.OrderBy(_ => Guid.NewGuid()).ToList());
        }

        public async Task Reset()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
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
                var entries = await GetEntries();
                var winningTicket = Tickets.RandomElementOrDefault(logger);
                var viewer = await viewerFeature.GetViewerByUserName(winningTicket);
                var isFollower = await viewerFeature.IsFollowerByUsername(winningTicket);
                var prize = await GetPrize();

                var message = await gameSettingsService.GetStringSetting(ModuleName, "WINNER", "(name) won the (prize) with a (chance)% of winning and (isfollowingCheck) following");
                var winningEntry = entries.Where(x => x.Username.Equals(winningTicket, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                var chance = winningEntry != null && entries.Sum(x => x.Tickets) > 0 ? (decimal)winningEntry.Tickets / (decimal)entries.Sum(x => x.Tickets) * 100 : 0;

                message = message
                    .Replace("(name)", viewer != null ? viewer.NameWithTitle() : winningTicket, StringComparison.OrdinalIgnoreCase)
                    .Replace("(prize)", prize, StringComparison.OrdinalIgnoreCase)
                    .Replace("(isFollowingCheck)", isFollower ? "is" : "is not", StringComparison.OrdinalIgnoreCase)
                    .Replace("(chance)", chance.ToString("0.00"), StringComparison.OrdinalIgnoreCase)
                    ;
#if DEBUG
                logger.LogInformation("DEBUG MODE: {message}", message);
#else
                logger.LogInformation("Drawing a ticket: {message}", message);
                await ServiceBackbone.SendChatMessage(message);
#endif
                await AddWinner(viewer, isFollower);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error drawing a ticket.");
            }
        }

        public async Task<List<GiveawayWinner>> PastWinners()
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var winners = await db.GiveawayWinners.GetAllAsync();
                return winners.OrderByDescending(x => x.WinningDate).ToList();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting past winners");
                return new List<GiveawayWinner> { };
            }
        }

        private async Task AddWinner(Viewer? viewer, bool isFollower)
        {
            if (viewer == null) return;

            var prize = await GetPrize();
            var prizeTier = await GetPrizeTier();
            await using (var scope = scopeFactory.CreateAsyncScope())
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
            await hubContext.Clients.All.SendAsync("Winners", Winners);
        }

        public async Task UpdateWinner(GiveawayWinner winner)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.GiveawayWinners.Update(winner);
            await db.SaveChangesAsync();

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
                    logger.LogWarning("Lock expired while waiting...");
                }
                amount = amount.ToLower();
                var viewerPoints = (await pointsSystem.GetUserPointsByUsernameAndGame(sender, ModuleName)).Points; //await ticketsFeature.GetViewerTickets(sender);
                if (amount == "max" || amount == "all")
                {
                    amount = (await pointsSystem.GetUserPointsByUsernameAndGame(sender, ModuleName)).Points.ToString();
                }
                var displayName = await viewerFeature.GetDisplayNameByUsername(sender);
                if (!Int32.TryParse(amount, out var points))
                {
                    var message = await gameSettingsService.GetStringSetting(ModuleName, "enter.notvalid", "please use a number or max/all when entering."); //language.Get("giveawayfeature.enter.notvalid");
                    if (!fromUi) await ServiceBackbone.SendChatMessage(displayName, message);

                    throw new SkipCooldownException(message);
                }
                if (points == 0 || points > viewerPoints)
                {
                    var message = await gameSettingsService.GetStringSetting(ModuleName, "enter.notenough", "you do not have enough or that many tickets to enter."); //language.Get("giveawayfeature.enter.notenough");
                    if (!fromUi) await ServiceBackbone.SendChatMessage(displayName, message);

                    throw new SkipCooldownException(message);
                }

                if (points < 0)
                {
                    var message = await gameSettingsService.GetStringSetting(ModuleName, "enter.minus", "don't be dumb."); //language.Get("giveawayfeature.enter.minus");
                    await ServiceBackbone.SendChatMessage(displayName, message);
                    throw new SkipCooldownException(message);
                }

                var enteredTickets = await GetEntriesCount(sender);
                if (points + enteredTickets > 1000000)
                {
                    points = 1000000 - points;
                    var message = await gameSettingsService.GetStringSetting(ModuleName, "enter.max", "Max entries is (maxallowed), so entering (amount) instead to max you out."); //language.Get("giveawayfeature.enter.max").Replace("(maxallowed)", "1,000,000").Replace("(amount)", points.ToString());
                    message = message.Replace("(maxallowed)", "1,000,000", StringComparison.OrdinalIgnoreCase).Replace("(amount)", points.ToString(), StringComparison.OrdinalIgnoreCase);
                    if (!fromUi)
                    {
                        await ServiceBackbone.SendChatMessage(displayName, message);
                    }

                    if (points == 0)
                    {
                        throw new SkipCooldownException("Unknown error, try via chat.");
                    }
                }

                //if (!(await ticketsFeature.RemoveTicketsFromViewerByUsername(sender, points)))
                if(!(await pointsSystem.RemovePointsFromUserByUsernameAndGame(sender, ModuleName, points)))
                {
                    var message = await gameSettingsService.GetStringSetting(ModuleName, "enter.failure", "failed to enter giveaway. Please try again."); //language.Get("giveawayfeature.enter.failure");
                    if (!fromUi)
                    {
                        await ServiceBackbone.SendChatMessage(displayName, message);
                    }

                    throw new SkipCooldownException(message);
                }

                using (var scope = scopeFactory.CreateAsyncScope())
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
                    var message = await gameSettingsService.GetStringSetting(ModuleName, "enter.success", "you have bought (amount) entries."); //language.Get("giveawayfeature.enter.success").Replace("(amount)", points.ToString());
                    message = message.Replace("(amount)", points.ToString("N0"), StringComparison.OrdinalIgnoreCase);
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
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var giveawayEntries = await db.GiveawayEntries.Find(x => x.Username.Equals(sender)).FirstOrDefaultAsync();
            giveawayEntries ??= new GiveawayEntry
            {
                Username = sender
            };
            return giveawayEntries.Tickets;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting {moduledname}", ModuleName);
            _timer.Elapsed += SendCurrentEntriesToAll;
            ServiceBackbone.StreamStarted += StreamStarted;
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopped {moduledname}", ModuleName);
            _timer.Elapsed -= SendCurrentEntriesToAll;
            ServiceBackbone.StreamStarted -= StreamStarted;
            return Task.CompletedTask;
        }
    }
}