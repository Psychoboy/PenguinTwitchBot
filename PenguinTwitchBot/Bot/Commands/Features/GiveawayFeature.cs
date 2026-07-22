using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Hubs;
using PenguinTwitchBot.Database.Bot.Models.Giveaway;
using PenguinTwitchBot.Extensions;
using PenguinTwitchBot.Database.Repository;
using Microsoft.AspNetCore.SignalR;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using System.Collections.ObjectModel;
using Timer = System.Timers.Timer;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Commands.Features
{
    public class GiveawayFeature(
        ILogger<GiveawayFeature> logger,
        IServiceBackbone serviceBackbone,
        IPointsSystem pointsSystem,
        IViewerFeature viewerFeature,
        IHubContext<MainHub> hubContext,
        IServiceScopeFactory scopeFactory,
        Application.Notifications.IPenguinDispatcher dispatcher,
        ICommandHandler commandHandler,
        IGameSettingsService gameSettingsService
            ) : BaseCommandService(serviceBackbone, commandHandler, "GiveawayFeature", dispatcher), IHostedService, IGiveawayFeature
    {
        private readonly List<string> Tickets = new();
        private readonly List<(string Username, int Tickets)> WeightedEntries = new();
        private int TotalWeightedTickets = 0;
        private DrawDebugSnapshot? LastDrawSnapshot;
        private readonly List<GiveawayWinner> Winners = new();
        private readonly string PrizeSettingName = "GiveawayPrize";
        private readonly string ImageSettingName = "GiveawayPrizeImage";
        private readonly string PrizeTierName = "GiveawayPrizeTier";
        private readonly string GiveawayAdditionalDetailsSettingName = "GiveawayAdditionalDetails";
        private readonly string GiveawayRulesSettingName = "GiveawayRules";
        private readonly string GiveawayCooldownsSettingName = "GiveawayCooldowns";
        private readonly string GiveawayTermsSettingName = "GiveawayTerms";
        private readonly string GiveawayPassiveEarningsSettingName = "GiveawayPassiveEarnings";
        private readonly string GiveawayMonteCarloFairnessEnabledSettingName = "GiveawayMonteCarloFairnessEnabled";
        private const int DefaultMonteCarloIterations = 100000;
        private const int MaxMonteCarloReports = 20;

        private static readonly string DefaultRulesMarkdown =
            "- Winners do **NOT** need to be present during the drawing\n" +
            "- Multiple accounts (botting) will result in **exclusion from all giveaways**\n" +
            "- Banned users (Discord or Twitch) are **not eligible**";

        private static readonly string DefaultCooldownsMarkdown =
            "After winning, you'll have a cooldown before being eligible for standard giveaways again " +
            "(special giveaways always eligible).\n\n" +
            "| Tier | Prize Value | Cooldown Period |\n" +
            "|------|-------------|-----------------|\n" +
            "| **Tier 1** | Less than $75 | 1 month or next giveaway |\n" +
            "| **Tier 2** | $75 \u2013 $115 | 2 months or next 2 giveaways |\n" +
            "| **Tier 3** | Greater than $115 | 3 months or next 3 giveaways |";

        private static readonly string DefaultTermsMarkdown =
            "- Winners have **48 hours** to whisper the streamer on Twitch to claim their prize\n" +
            "- Winners announced live on stream and in [Discord](https://discord.gg/4zVq4DyBFS) (#giveaway-winners channel)\n" +
            "- **Subscribers** auto-claim and will be whispered directly\n" +
            "- The streamer reserves the right to change giveaways at any time\n" +
            "- Must be **18+** to participate\n" +
            "- Void where prohibited by law";
        private static readonly ConcurrentDictionary<string, Lazy<SemaphoreSlim>> UserLocks = new();
        private readonly SemaphoreSlim fairnessReportLock = new(1, 1);
        private readonly List<GiveawayFairnessReport> fairnessReports = new();
        private readonly Timer _timer = new(TimeSpan.FromSeconds(5).TotalMilliseconds);
        private static readonly Prometheus.Gauge NumberOfTicketsEntered = Prometheus.Metrics.CreateGauge("number_of_tickets_entered", "Number of Tickets entered since last stream start", labelNames: new[] { "viewer" });

        private bool isClosed = false;

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
            await RegisterDefaultCommand("open", this, moduleName, Rank.Streamer);
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
                            await ServiceBackbone.ResponseWithMessage(e, await gameSettingsService.GetStringSetting(ModuleName, "help.enter", "To enter tickets, please use !enter AMOUNT/MAX/ALL"));
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
                case "open":
                    {
                        await Open();
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

        public bool IsClosed()
        {
            return isClosed;
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
            return await db.GiveawayExclusions.Find(x => x.ExpireDateTime == null || x.ExpireDateTime > DateTime.UtcNow).ToListAsync();
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

        public async Task<string> GetRules()
        {
            return await gameSettingsService.GetStringSetting(ModuleName, GiveawayRulesSettingName, DefaultRulesMarkdown);
        }

        public async Task SetRules(string value)
        {
            await gameSettingsService.SetStringSetting(ModuleName, GiveawayRulesSettingName, value);
        }

        public async Task<string> GetCooldowns()
        {
            return await gameSettingsService.GetStringSetting(ModuleName, GiveawayCooldownsSettingName, DefaultCooldownsMarkdown);
        }

        public async Task SetCooldowns(string value)
        {
            await gameSettingsService.SetStringSetting(ModuleName, GiveawayCooldownsSettingName, value);
        }

        public async Task<string> GetTerms()
        {
            return await gameSettingsService.GetStringSetting(ModuleName, GiveawayTermsSettingName, DefaultTermsMarkdown);
        }

        public async Task SetTerms(string value)
        {
            await gameSettingsService.SetStringSetting(ModuleName, GiveawayTermsSettingName, value);
        }

        public async Task<string> GetPassiveEarnings()
        {
            return await gameSettingsService.GetStringSetting(ModuleName, GiveawayPassiveEarningsSettingName, "");
        }

        public async Task SetPassiveEarnings(string value)
        {
            await gameSettingsService.SetStringSetting(ModuleName, GiveawayPassiveEarningsSettingName, value);
        }

        public async Task<bool> GetMonteCarloFairnessEnabled()
        {
            return await gameSettingsService.GetBoolSetting(ModuleName, GiveawayMonteCarloFairnessEnabledSettingName, false);
        }

        public async Task SetMonteCarloFairnessEnabled(bool enabled)
        {
            await gameSettingsService.SetBoolSetting(ModuleName, GiveawayMonteCarloFairnessEnabledSettingName, enabled);
        }

        public async Task<IReadOnlyList<GiveawayFairnessReport>> GetMonteCarloFairnessReports()
        {
            await fairnessReportLock.WaitAsync();
            try
            {
                return new ReadOnlyCollection<GiveawayFairnessReport>(fairnessReports.ToList());
            }
            finally
            {
                fairnessReportLock.Release();
            }
        }

        public async Task<GiveawayFairnessReport?> RunMonteCarloFairnessReport(int? iterations = null)
        {
            if (TotalWeightedTickets == 0 || WeightedEntries.Count == 0)
            {
                await RebuildWeightedPool();
            }

            var boundedIterations = Math.Max(1000, Math.Min(iterations ?? DefaultMonteCarloIterations, 2_000_000));

            return await GenerateAndStoreMonteCarloFairnessReport(boundedIterations);
        }

        private async Task<GiveawayFairnessReport?> GenerateAndStoreMonteCarloFairnessReport(int iterations)
        {
            if (TotalWeightedTickets == 0 || WeightedEntries.Count == 0)
            {
                return null;
            }

            var entries = WeightedEntries.ToList();
            var totalTickets = TotalWeightedTickets;
            var report = GenerateMonteCarloFairnessReport(entries, totalTickets, iterations, null);

            await fairnessReportLock.WaitAsync();
            try
            {
                fairnessReports.Insert(0, report);
                if (fairnessReports.Count > MaxMonteCarloReports)
                {
                    fairnessReports.RemoveRange(MaxMonteCarloReports, fairnessReports.Count - MaxMonteCarloReports);
                }
            }
            finally
            {
                fairnessReportLock.Release();
            }

            logger.LogInformation(
                "Giveaway Monte Carlo fairness report generated: iterations={Iterations}, entrants={Entrants}, totalTickets={TotalTickets}, maxAbsDelta={MaxAbsDelta}%",
                report.Iterations,
                report.Results.Count,
                report.TotalTickets,
                report.MaxAbsoluteDeltaPercent.ToString("0.000", CultureInfo.InvariantCulture));

            return report;
        }

        private async Task RebuildWeightedPool()
        {
            Tickets.Clear();
            WeightedEntries.Clear();
            TotalWeightedTickets = 0;

            var entries = await GetEntries();
            var exclusions = await GetExclusions();
            var excludedUsers = exclusions
                .Select(x => x.Username)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries)
            {
                if (excludedUsers.Contains(entry.Username))
                {
                    continue;
                }

                if (entry.Tickets <= 0)
                {
                    continue;
                }

                // Keep a compact list for visibility while preserving weighted draw odds.
                Tickets.Add(entry.Username);
                WeightedEntries.Add((entry.Username, entry.Tickets));
                TotalWeightedTickets += entry.Tickets;
            }
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

        internal sealed record DrawDebugSnapshot(
            DateTime DrawTimeUtc,
            int WinningTicketIndex,
            int TotalTickets,
            int EligibleEntrants,
            string WinnerUsername,
            int WinnerTickets,
            string PoolFingerprint
        );

        public async Task Close()
        {
            isClosed = true;
            LastDrawSnapshot = null;
            await RebuildWeightedPool();

            logger.LogInformation(
                "Giveaway pool closed with {Entrants} eligible entrants and {Tickets} total tickets. Fairness report: {FairnessReport}",
                WeightedEntries.Count,
                TotalWeightedTickets,
                BuildFairnessReport(WeightedEntries, TotalWeightedTickets));

            if (await GetMonteCarloFairnessEnabled())
            {
                _ = await GenerateAndStoreMonteCarloFairnessReport(DefaultMonteCarloIterations);
            }

            await hubContext.Clients.All.SendAsync("GiveawayIsClosed", isClosed);
        }

        public async Task Open()
        {
            isClosed = false;
            Tickets.Clear();
            WeightedEntries.Clear();
            TotalWeightedTickets = 0;
            LastDrawSnapshot = null;
            await hubContext.Clients.All.SendAsync("GiveawayIsClosed", isClosed);
        }

        public async Task Reset()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await db.GiveawayEntries.ExecuteDeleteAllAsync();
            await db.SaveChangesAsync();
            Tickets.Clear();
            WeightedEntries.Clear();
            TotalWeightedTickets = 0;
            LastDrawSnapshot = null;
            isClosed = false;
            await hubContext.Clients.All.SendAsync("GiveawayIsClosed", isClosed);
        }

        public async Task Draw()
        {
            if (TotalWeightedTickets == 0)
            {
                await Close();
            }

            try
            {
                if (TotalWeightedTickets == 0 || WeightedEntries.Count == 0)
                {
                    var noEntriesMessage = await gameSettingsService.GetStringSetting(ModuleName, "draw.noentries", "No eligible entries for the giveaway draw.");
                    await ServiceBackbone.SendChatMessage(noEntriesMessage);
                    return;
                }

                var winningTicketIndex = RandomNumberGenerator.GetInt32(TotalWeightedTickets);
                var winner = SelectWinnerFromWeightedEntries(WeightedEntries, TotalWeightedTickets, winningTicketIndex);
                if (winner == null)
                {
                    logger.LogWarning("Weighted draw failed to resolve a winner despite non-empty pool.");
                    return;
                }

                var winningTicket = winner.Value.Username;
                var winningEntryTickets = winner.Value.Tickets;

                LastDrawSnapshot = new DrawDebugSnapshot(
                    DateTime.UtcNow,
                    winningTicketIndex,
                    TotalWeightedTickets,
                    WeightedEntries.Count,
                    winningTicket,
                    winningEntryTickets,
                    BuildWeightedPoolFingerprint(WeightedEntries, TotalWeightedTickets));

                logger.LogInformation(
                    "Giveaway draw debug replay: index={Index}, totalTickets={TotalTickets}, entrants={Entrants}, winner={Winner}, winnerTickets={WinnerTickets}",
                    LastDrawSnapshot.WinningTicketIndex,
                    LastDrawSnapshot.TotalTickets,
                    LastDrawSnapshot.EligibleEntrants,
                    LastDrawSnapshot.WinnerUsername,
                    LastDrawSnapshot.WinnerTickets);

                var viewer = await viewerFeature.GetViewerByUserName(winningTicket);
                var isFollower = await viewerFeature.IsFollowerByUsername(winningTicket);
                var prize = await GetPrize();

                var message = await gameSettingsService.GetStringSetting(ModuleName, "WINNER", "(name) won the (prize) with a (chance)% of winning and (isfollowingCheck) following");
                var chance = TotalWeightedTickets > 0 ? (decimal)winningEntryTickets / (decimal)TotalWeightedTickets * 100 : 0;

                message = message
                    .Replace("(name)", viewer != null ? viewer.NameWithTitle() : winningTicket, StringComparison.OrdinalIgnoreCase)
                    .Replace("(prize)", prize, StringComparison.OrdinalIgnoreCase)
                    .Replace("(isFollowingCheck)", isFollower ? "is" : "is not", StringComparison.OrdinalIgnoreCase)
                    .Replace("(chance)", chance.ToString("0.00"), StringComparison.OrdinalIgnoreCase)
                    ;
#if DEBUG
                message = "[DEBUG] " + message;
#endif
                logger.LogInformation("Drawing a ticket: {message}", message);
                await ServiceBackbone.SendChatMessage(message);
                await AddWinner(viewer, isFollower);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error drawing a ticket.");
            }
        }

        internal static (string Username, int Tickets)? SelectWinnerFromWeightedEntries(
            IReadOnlyList<(string Username, int Tickets)> weightedEntries,
            int totalWeightedTickets,
            int winningTicketIndex)
        {
            if (weightedEntries == null || weightedEntries.Count == 0 || totalWeightedTickets <= 0)
            {
                return null;
            }

            if (winningTicketIndex < 0 || winningTicketIndex >= totalWeightedTickets)
            {
                throw new ArgumentOutOfRangeException(nameof(winningTicketIndex));
            }

            var runningTotal = 0;
            foreach (var entry in weightedEntries)
            {
                if (entry.Tickets <= 0)
                {
                    continue;
                }

                runningTotal += entry.Tickets;
                if (winningTicketIndex < runningTotal)
                {
                    return entry;
                }
            }

            return null;
        }

        internal static string BuildWeightedPoolFingerprint(
            IReadOnlyList<(string Username, int Tickets)> weightedEntries,
            int totalWeightedTickets)
        {
            var normalized = string.Join("|", weightedEntries
                .OrderBy(x => x.Username, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Tickets)
                .Select(x => $"{x.Username}:{x.Tickets}"));

            var payload = $"{totalWeightedTickets}|{weightedEntries.Count}|{normalized}";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(bytes);
        }

        internal static string BuildFairnessReport(
            IReadOnlyList<(string Username, int Tickets)> weightedEntries,
            int totalWeightedTickets,
            int maxUsers = 10)
        {
            if (weightedEntries == null || weightedEntries.Count == 0 || totalWeightedTickets <= 0)
            {
                return "no eligible entries";
            }

            var report = weightedEntries
                .OrderByDescending(x => x.Tickets)
                .ThenBy(x => x.Username, StringComparer.OrdinalIgnoreCase)
                .Take(maxUsers)
                .Select(x =>
                {
                    var pct = ((decimal)x.Tickets / totalWeightedTickets * 100m).ToString("0.00", CultureInfo.InvariantCulture);
                    return $"{x.Username}:{x.Tickets}({pct}%)";
                });

            return string.Join(", ", report);
        }

        internal static GiveawayFairnessReport GenerateMonteCarloFairnessReport(
            IReadOnlyList<(string Username, int Tickets)> weightedEntries,
            int totalWeightedTickets,
            int iterations,
            int? seed)
        {
            if (weightedEntries == null || weightedEntries.Count == 0 || totalWeightedTickets <= 0)
            {
                throw new ArgumentException("Cannot generate fairness report without eligible entries.");
            }

            if (iterations <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(iterations));
            }

            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var random = seed.HasValue ? new Random(seed.Value) : null;

            for (var i = 0; i < iterations; i++)
            {
                var winnerIndex = random == null
                    ? RandomNumberGenerator.GetInt32(totalWeightedTickets)
                    : random.Next(totalWeightedTickets);

                var winner = SelectWinnerFromWeightedEntries(weightedEntries, totalWeightedTickets, winnerIndex);
                if (winner == null)
                {
                    continue;
                }

                var username = winner.Value.Username;
                counts[username] = counts.TryGetValue(username, out var current) ? current + 1 : 1;
            }

            var resultRows = weightedEntries
                .OrderByDescending(x => x.Tickets)
                .ThenBy(x => x.Username, StringComparer.OrdinalIgnoreCase)
                .Select(entry =>
                {
                    var observedHits = counts.TryGetValue(entry.Username, out var hits) ? hits : 0;
                    var expectedPercent = (decimal)entry.Tickets / totalWeightedTickets * 100m;
                    var observedPercent = (decimal)observedHits / iterations * 100m;
                    var delta = Math.Abs(expectedPercent - observedPercent);

                    return new GiveawayFairnessUserResult(
                        entry.Username,
                        entry.Tickets,
                        decimal.Round(expectedPercent, 4),
                        decimal.Round(observedPercent, 4),
                        decimal.Round(delta, 4));
                })
                .ToList();

            var maxDelta = resultRows.Count == 0 ? 0m : resultRows.Max(x => x.AbsoluteDeltaPercent);

            return new GiveawayFairnessReport(
                DateTime.UtcNow,
                iterations,
                totalWeightedTickets,
                BuildWeightedPoolFingerprint(weightedEntries, totalWeightedTickets),
                decimal.Round(maxDelta, 4),
                resultRows);
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
            var userLock = UserLocks.GetOrAdd(sender, _ => new Lazy<SemaphoreSlim>(() => new SemaphoreSlim(1, 1))).Value;
            await userLock.WaitAsync();
            try
            {
                if (isClosed) 
                {
                    var message = await gameSettingsService.GetStringSetting(ModuleName, "enter.closed", "the giveaway is closed and not accepting entries."); 
                    if (!fromUi) await ServiceBackbone.SendChatMessage(sender, message);
                    throw new SkipCooldownException(message);
                }

                amount = amount.ToLower();
                var viewerPoints = (await pointsSystem.GetUserPointsByUsernameAndGame(sender, ModuleName)).Points;
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

                if(viewerPoints - points < 0)
                {
                    var message = await gameSettingsService.GetStringSetting(ModuleName, "enter.notenough", "you do not have enough or that many tickets to enter."); //language.Get("giveawayfeature.enter.notenough");
                    if (!fromUi) await ServiceBackbone.SendChatMessage(displayName, message);

                    throw new SkipCooldownException(message);
                }
                
                
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
                userLock.Release();
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
