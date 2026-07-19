using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    /// <summary>
    /// Core fishing service for fish types, catches, gold, and settings management.
    /// Use specialized services for shop, inventory, gameplay, analytics, and leaderboards.
    /// </summary>
    public class FishingService : IFishingService
    {
        private const string FishingTournamentStartTriggerName = "FishingTournament.Start";
        private const string FishingTournamentEndTriggerName = "FishingTournament.End";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<FishingService> _logger;
        private readonly IPointsSystem _pointsSystem;

        public FishingService(IServiceScopeFactory scopeFactory, ILogger<FishingService> logger, IPointsSystem pointsSystem)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _pointsSystem = pointsSystem;
        }

        #region Fish Type Management

        public async Task<List<FishType>> GetAllFishTypes()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await context.FishTypes
                .OrderBy(f => f.Name)
                .ToListAsync();
        }

        public async Task<List<FishType>> GetFishTypesWithCatches()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await context.FishTypes
                .Where(f => f.Enabled && context.FishCatches.Any(c => c.FishTypeId == f.Id))
                .OrderBy(f => f.Name)
                .ToListAsync();
        }

        public async Task<FishType?> GetFishTypeById(int id)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await context.FishTypes.FindAsync(id);
        }

        public async Task AddFishType(FishType fishType)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.FishTypes.Add(fishType);
            await context.SaveChangesAsync();
        }

        public async Task UpdateFishType(FishType fishType)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.FishTypes.Update(fishType);
            await context.SaveChangesAsync();
        }

        public async Task DeleteFishType(int id)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var fishType = await context.FishTypes.FindAsync(id);
            if (fishType != null)
            {
                context.FishTypes.Remove(fishType);
                await context.SaveChangesAsync();
            }
        }

        #endregion

        #region Fish Catch Queries

        public async Task<List<FishCatch>> GetTopCatchesForFishType(int fishTypeId, int count = 10)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await context.FishCatches
                .Include(c => c.FishType)
                .Where(c => c.FishTypeId == fishTypeId)
                .OrderByDescending(c => c.Stars)
                .ThenByDescending(c => c.Weight)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<FishCatch>> GetUserCatches(string userId, int count = 50)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await context.FishCatches
                .Include(c => c.FishType)
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CaughtAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<FishCatch?> GetUserBestCatchForFishType(string userId, int fishTypeId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await context.FishCatches
                .Include(c => c.FishType)
                .Where(c => c.UserId == userId && c.FishTypeId == fishTypeId)
                .OrderByDescending(c => c.Stars)
                .ThenByDescending(c => c.Weight)
                .FirstOrDefaultAsync();
        }

        public async Task<int> GetUserCatchCountForFishType(string userId, int fishTypeId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await context.FishCatches
                .Where(c => c.UserId == userId && c.FishTypeId == fishTypeId)
                .CountAsync();
        }

        public async Task<Dictionary<int, FishCatch>> GetUserBestCatchesForAllFishTypes(string userId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var bestCatches = await context.FishCatches
                .Include(c => c.FishType)
                .Where(c => c.UserId == userId)
                .GroupBy(c => c.FishTypeId)
                .Select(g => g.OrderByDescending(c => c.Stars)
                             .ThenByDescending(c => c.Weight)
                             .ThenByDescending(c => c.CaughtAt)
                             .FirstOrDefault())
                .OfType<FishCatch>()
                .ToListAsync();

            return bestCatches.ToDictionary(c => c.FishTypeId, c => c);
        }

        public async Task<Dictionary<int, int>> GetUserCatchCountsForAllFishTypes(string userId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var counts = await context.FishCatches
                .Where(c => c.UserId == userId)
                .GroupBy(c => c.FishTypeId)
                .Select(g => new { FishTypeId = g.Key, Count = g.Count() })
                .ToListAsync();

            return counts.ToDictionary(c => c.FishTypeId, c => c.Count);
        }

        public async Task<List<FishingTournament>> GetAllFishingTournaments(int count = 100)
        {
            count = Math.Max(1, Math.Min(count, 500));

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return await context.FishingTournaments
                .AsNoTracking()
                .AsSplitQuery()
                .Include(t => t.EntryFeePointType)
                .Include(t => t.EligibleFish)
                    .ThenInclude(e => e.FishType)
                .Include(t => t.RewardRules)
                    .ThenInclude(r => r.PointType)
                .Include(t => t.RewardRules)
                    .ThenInclude(r => r.TargetFishType)
                .OrderByDescending(t => t.StartsAtUtc)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<FishingTournament>> GetCurrentFishingTournaments()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return await context.FishingTournaments
                .AsNoTracking()
                .AsSplitQuery()
                .Include(t => t.EntryFeePointType)
                .Include(t => t.EligibleFish)
                    .ThenInclude(e => e.FishType)
                .Include(t => t.RewardRules)
                    .ThenInclude(r => r.PointType)
                .Include(t => t.RewardRules)
                    .ThenInclude(r => r.TargetFishType)
                .Where(t => t.Enabled && (t.Status == FishingTournamentStatus.Active || t.Status == FishingTournamentStatus.Scheduled))
                .OrderBy(t => t.Status)
                .ThenBy(t => t.StartsAtUtc)
                .ToListAsync();
        }

        public async Task<List<FishingTournament>> GetPastFishingTournaments(int count = 25)
        {
            count = Math.Max(1, Math.Min(count, 100));

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return await context.FishingTournaments
                .AsNoTracking()
                .AsSplitQuery()
                .Include(t => t.EntryFeePointType)
                .Include(t => t.EligibleFish)
                    .ThenInclude(e => e.FishType)
                .Include(t => t.RewardRules)
                    .ThenInclude(r => r.PointType)
                .Include(t => t.RewardRules)
                    .ThenInclude(r => r.TargetFishType)
                .Where(t => t.Status == FishingTournamentStatus.Completed || t.Status == FishingTournamentStatus.Cancelled)
                .OrderByDescending(t => t.EndsAtUtc ?? t.StartsAtUtc)
                .Take(count)
                .ToListAsync();
        }

        public async Task<FishingTournament?> GetFishingTournamentById(int id)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return await context.FishingTournaments
                .AsSplitQuery()
                .Include(t => t.EntryFeePointType)
                .Include(t => t.EligibleFish)
                    .ThenInclude(e => e.FishType)
                .Include(t => t.RewardRules)
                    .ThenInclude(r => r.PointType)
                .Include(t => t.RewardRules)
                    .ThenInclude(r => r.TargetFishType)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<List<FishingTournamentStanding>> GetFishingTournamentStandings(int tournamentId, int count = 10)
        {
            count = Math.Max(1, Math.Min(count, 100));

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var tournament = await context.FishingTournaments
                .AsNoTracking()
                .AsSplitQuery()
                .Include(t => t.EligibleFish)
                    .ThenInclude(e => e.FishType)
                .FirstOrDefaultAsync(t => t.Id == tournamentId);

            if (tournament == null)
            {
                return [];
            }

            var catches = await GetTournamentCatches(context, tournament, null, useLinkedCatchesOnly: true);
            if (catches.Count == 0)
            {
                return [];
            }

            var targetFishTypeId = tournament.PrimaryScoreCategory == FishingTournamentScoreCategory.SpecificFish && tournament.EligibleFish.Count == 1
                ? (int?)tournament.EligibleFish.First().FishTypeId
                : null;

            return CalculateStandings(catches, tournament.PrimaryScoreCategory, targetFishTypeId)
                .Take(count)
                .Select((standing, index) => new FishingTournamentStanding
                {
                    Rank = index + 1,
                    UserId = standing.UserId,
                    Username = standing.Username,
                    Score = standing.Score,
                    CatchCount = standing.CatchCount,
                    LastCaughtAtUtc = standing.LastCaughtAtUtc
                })
                .ToList();
        }

        public async Task<FishingTournament?> StartFishingTournament(int id)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var tournament = await context.FishingTournaments
                .AsSplitQuery()
                .Include(t => t.EntryFeePointType)
                .Include(t => t.EligibleFish)
                    .ThenInclude(e => e.FishType)
                .Include(t => t.RewardRules)
                    .ThenInclude(r => r.PointType)
                .Include(t => t.RewardRules)
                    .ThenInclude(r => r.TargetFishType)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tournament == null)
            {
                return null;
            }

            if (tournament.Status is FishingTournamentStatus.Completed or FishingTournamentStatus.Cancelled)
            {
                return tournament;
            }

            var now = DateTime.UtcNow;
            var wasActive = tournament.Status == FishingTournamentStatus.Active;
            tournament.Enabled = true;
            tournament.Status = FishingTournamentStatus.Active;
            tournament.StartsAtUtc = now;
            tournament.EndsAtUtc = now.AddMinutes(Math.Max(1, tournament.RunDurationMinutes));

            await context.SaveChangesAsync();

            if (!wasActive)
            {
                await TriggerFishingTournamentLifecycleActionsAsync(tournament, TriggerTypes.FishingTournamentStart, FishingTournamentStartTriggerName);
            }

            return tournament;
        }

        public async Task<FishingTournament?> CloneAndStartFishingTournament(int templateTournamentId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var template = await context.FishingTournaments
                .AsNoTracking()
                .AsSplitQuery()
                .Include(t => t.EligibleFish)
                .Include(t => t.RewardRules)
                .FirstOrDefaultAsync(t => t.Id == templateTournamentId);

            if (template == null)
            {
                return null;
            }

            var clonedTournament = new FishingTournament
            {
                Name = string.IsNullOrWhiteSpace(template.Name)
                    ? $"Tournament {DateTime.UtcNow:yyyy-MM-dd HH:mm}"
                    : $"{template.Name} ({DateTime.UtcNow:yyyy-MM-dd HH:mm})",
                Description = template.Description,
                Enabled = true,
                Status = FishingTournamentStatus.Scheduled,
                PrimaryScoreCategory = template.PrimaryScoreCategory,
                StartsAtUtc = null,
                EndsAtUtc = null,
                AutoScheduleEnabled = false,
                AutoScheduleCron = string.Empty,
                RunDurationMinutes = Math.Max(1, template.RunDurationMinutes),
                EntryFeeAmount = template.EntryFeeAmount,
                EntryFeePointTypeId = template.EntryFeePointTypeId,
                EligibleFish = template.EligibleFish
                    .Select(fish => new FishingTournamentFishType { FishTypeId = fish.FishTypeId })
                    .ToList(),
                RewardRules = template.RewardRules
                    .Select(rule => new FishingTournamentRewardRule
                    {
                        ScoreCategory = rule.ScoreCategory,
                        TargetFishTypeId = rule.TargetFishTypeId,
                        RewardKind = rule.RewardKind,
                        Placement = rule.Placement,
                        Points = rule.Points,
                        EntryFeePercentage = rule.EntryFeePercentage,
                        PointTypeId = rule.PointTypeId,
                        Enabled = rule.Enabled
                    })
                    .ToList()
            };

            context.FishingTournaments.Add(clonedTournament);
            await context.SaveChangesAsync();

            return await StartFishingTournament(clonedTournament.Id);
        }

        public async Task<FishingTournament?> ReopenFishingTournament(int id)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var tournament = await context.FishingTournaments
                .AsSplitQuery()
                .Include(t => t.EntryFeePointType)
                .Include(t => t.EligibleFish)
                    .ThenInclude(e => e.FishType)
                .Include(t => t.RewardRules)
                    .ThenInclude(r => r.PointType)
                .Include(t => t.RewardRules)
                    .ThenInclude(r => r.TargetFishType)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tournament == null)
            {
                return null;
            }

            if (tournament.Status is not (FishingTournamentStatus.Completed or FishingTournamentStatus.Cancelled))
            {
                return tournament;
            }

            var linkedCatches = await context.FishingTournamentCatches
                .Where(link => link.FishingTournamentId == id)
                .ToListAsync();

            if (linkedCatches.Count > 0)
            {
                context.FishingTournamentCatches.RemoveRange(linkedCatches);
            }

            tournament.Enabled = true;
            tournament.Status = FishingTournamentStatus.Scheduled;
            tournament.StartsAtUtc = null;
            tournament.EndsAtUtc = null;

            await context.SaveChangesAsync();
            return tournament;
        }

        public async Task<FishingTournament> SaveFishingTournament(FishingTournament tournament)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var persistedTournament = await context.FishingTournaments
                .AsSplitQuery()
                .Include(t => t.EligibleFish)
                .Include(t => t.RewardRules)
                .FirstOrDefaultAsync(t => t.Id == tournament.Id);

            if (persistedTournament == null)
            {
                context.FishingTournaments.Add(tournament);
                await context.SaveChangesAsync();
                return tournament;
            }

            persistedTournament.Name = tournament.Name;
            persistedTournament.Description = tournament.Description;
            persistedTournament.Enabled = tournament.Enabled;
            persistedTournament.Status = tournament.Status;
            persistedTournament.PrimaryScoreCategory = tournament.PrimaryScoreCategory;
            persistedTournament.StartsAtUtc = tournament.StartsAtUtc;
            persistedTournament.EndsAtUtc = tournament.EndsAtUtc;
            persistedTournament.AutoScheduleEnabled = tournament.AutoScheduleEnabled;
            persistedTournament.AutoScheduleCron = tournament.AutoScheduleCron;
            persistedTournament.RunDurationMinutes = tournament.RunDurationMinutes;
            persistedTournament.EntryFeeAmount = tournament.EntryFeeAmount;
            persistedTournament.EntryFeePointTypeId = tournament.EntryFeePointTypeId;

            context.FishingTournamentFishTypes.RemoveRange(persistedTournament.EligibleFish);
            context.FishingTournamentRewardRules.RemoveRange(persistedTournament.RewardRules);

            persistedTournament.EligibleFish = tournament.EligibleFish
                .Select(fish => new FishingTournamentFishType
                {
                    FishTypeId = fish.FishTypeId
                })
                .ToList();

            persistedTournament.RewardRules = tournament.RewardRules
                .Select(rule => new FishingTournamentRewardRule
                {
                    ScoreCategory = rule.ScoreCategory,
                    TargetFishTypeId = rule.TargetFishTypeId,
                    RewardKind = rule.RewardKind,
                    Placement = rule.Placement,
                    Points = rule.Points,
                    EntryFeePercentage = rule.EntryFeePercentage,
                    PointTypeId = rule.PointTypeId,
                    Enabled = rule.Enabled
                })
                .ToList();

            await context.SaveChangesAsync();
            return persistedTournament;
        }

        public async Task<FishingTournament?> EndFishingTournament(int id)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var tournament = await context.FishingTournaments
                .AsSplitQuery()
                .Include(t => t.EntryFeePointType)
                .Include(t => t.EligibleFish)
                    .ThenInclude(e => e.FishType)
                .Include(t => t.RewardRules)
                    .ThenInclude(r => r.PointType)
                .Include(t => t.RewardRules)
                    .ThenInclude(r => r.TargetFishType)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tournament == null)
            {
                return null;
            }

            // Only settle once: skip if already Completed or Cancelled.
            if (tournament.Status is FishingTournamentStatus.Completed or FishingTournamentStatus.Cancelled)
            {
                return tournament;
            }

            var rewardWinners = await SettleFishingTournamentRewards(tournament, DateTime.UtcNow);

            tournament.Status = FishingTournamentStatus.Completed;
            tournament.Enabled = false;
            tournament.EndsAtUtc = DateTime.UtcNow;

            await context.SaveChangesAsync();

            await TriggerFishingTournamentLifecycleActionsAsync(
                tournament,
                TriggerTypes.FishingTournamentEnd,
                FishingTournamentEndTriggerName,
                rewardWinners);

            return tournament;
        }

        private async Task<List<TournamentRewardWinner>> SettleFishingTournamentRewards(FishingTournament tournament, DateTime settlementEndUtc)
        {
            var winners = new List<TournamentRewardWinner>();

            if (tournament.RewardRules.Count == 0)
            {
                return winners;
            }

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var catches = await GetTournamentCatches(context, tournament, settlementEndUtc, useLinkedCatchesOnly: false);

            if (catches.Count == 0)
            {
                return winners;
            }

            foreach (var rewardRule in tournament.RewardRules.Where(rule => rule.Enabled).OrderBy(rule => rule.Placement))
            {
                var standings = CalculateStandings(catches, rewardRule)
                    .Take(rewardRule.Placement)
                    .ToList();

                var winner = standings.LastOrDefault();
                if (winner == null)
                {
                    continue;
                }

                var rewardAmount = rewardRule.RewardKind == FishingTournamentRewardKind.EntryFeePercentage
                    ? Math.Max(0L, (long)Math.Round((tournament.EntryFeeAmount ?? 0L) * ((rewardRule.EntryFeePercentage ?? 0) / 100.0), MidpointRounding.AwayFromZero))
                    : Math.Max(0L, rewardRule.Points);

                if (rewardAmount <= 0)
                {
                    continue;
                }

                await _pointsSystem.AddPointsByUserId(winner.UserId, rewardRule.PointTypeId, rewardAmount);

                winners.Add(new TournamentRewardWinner
                {
                    UserId = winner.UserId,
                    Username = winner.Username,
                    Placement = rewardRule.Placement,
                    PointTypeId = rewardRule.PointTypeId,
                    PointTypeName = rewardRule.PointType?.Name ?? string.Empty,
                    ScoreCategory = rewardRule.ScoreCategory,
                    RewardKind = rewardRule.RewardKind,
                    RewardAmount = rewardAmount
                });

                _logger.LogInformation(
                    "Settled tournament {TournamentId} reward for {Username}: placement {Placement}, category {Category}, amount {Amount} on point type {PointTypeId}",
                    tournament.Id,
                    winner.Username,
                    rewardRule.Placement,
                    rewardRule.ScoreCategory,
                    rewardAmount,
                    rewardRule.PointTypeId);
            }

            return winners;
        }

        private async Task TriggerFishingTournamentLifecycleActionsAsync(
            FishingTournament tournament,
            TriggerTypes triggerType,
            string triggerName,
            List<TournamentRewardWinner>? rewardWinners = null)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var actionManagement = scope.ServiceProvider.GetRequiredService<IActionManagementService>();
                var actionService = scope.ServiceProvider.GetRequiredService<IAction>();

                var actions = await actionManagement.GetActionsByTriggerTypeAndNameAsync(triggerType, triggerName);
                if (actions.Count == 0)
                {
                    return;
                }

                var eligibleFishNames = tournament.EligibleFish
                    .Select(item => item.FishType?.Name)
                    .OfType<string>()
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var rewardWinnerList = rewardWinners ?? [];

                foreach (var action in actions)
                {
                    var hasMatchingEnabledTrigger = action.Triggers.Any(trigger =>
                        trigger.Type == triggerType &&
                        trigger.Enabled &&
                        string.Equals(trigger.Name, triggerName, StringComparison.Ordinal));

                    if (!hasMatchingEnabledTrigger)
                    {
                        continue;
                    }

                    var variables = new ConcurrentDictionary<string, string>();
                    PopulateTournamentLifecycleVariables(variables, tournament, eligibleFishNames, rewardWinnerList);

                    await actionService.EnqueueAction(variables, action);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering fishing tournament lifecycle action for trigger {TriggerName}", triggerName);
            }
        }

        private static void PopulateTournamentLifecycleVariables(
            ConcurrentDictionary<string, string> variables,
            FishingTournament tournament,
            List<string> eligibleFishNames,
            List<TournamentRewardWinner> rewardWinners)
        {
            variables["fishing_tournament_id"] = tournament.Id.ToString();
            variables["fishing_tournament_name"] = tournament.Name;
            variables["fishing_tournament_description"] = tournament.Description;
            variables["fishing_tournament_status"] = tournament.Status.ToString();
            variables["fishing_tournament_enabled"] = tournament.Enabled.ToString().ToLowerInvariant();
            variables["fishing_tournament_primary_score_category"] = tournament.PrimaryScoreCategory.ToString();
            variables["fishing_tournament_starts_at_utc"] = tournament.StartsAtUtc?.ToString("O") ?? string.Empty;
            variables["fishing_tournament_ends_at_utc"] = tournament.EndsAtUtc?.ToString("O") ?? string.Empty;

            variables["fishing_tournament_eligible_fish_count"] = eligibleFishNames.Count.ToString();
            variables["fishing_tournament_eligible_fish_names"] = string.Join(", ", eligibleFishNames);
            variables["fishing_tournament_eligible_fish_preview"] = string.Join(", ", eligibleFishNames.Take(3));
            variables["fishing_tournament_eligible_fish_over_three"] = (eligibleFishNames.Count > 3).ToString().ToLowerInvariant();

            variables["fishing_tournament_reward_winner_count"] = rewardWinners.Count.ToString();
            variables["fishing_tournament_reward_winner_names"] = string.Join(", ", rewardWinners.Select(winner => winner.Username).Distinct(StringComparer.OrdinalIgnoreCase));
            variables["fishing_tournament_reward_winner_ids"] = string.Join(",", rewardWinners.Select(winner => winner.UserId).Distinct(StringComparer.OrdinalIgnoreCase));
            variables["fishing_tournament_reward_summary"] = string.Join("; ", rewardWinners.Select(winner =>
                $"#{winner.Placement} {winner.Username} won {winner.RewardAmount} {(string.IsNullOrWhiteSpace(winner.PointTypeName) ? $"PointType:{winner.PointTypeId}" : winner.PointTypeName)}"));
        }

        private static List<TournamentStanding> CalculateStandings(List<FishCatch> catches, FishingTournamentRewardRule rewardRule)
        {
            return CalculateStandings(catches, rewardRule.ScoreCategory, rewardRule.TargetFishTypeId);
        }

        private static List<TournamentStanding> CalculateStandings(List<FishCatch> catches, FishingTournamentScoreCategory scoreCategory, int? targetFishTypeId = null)
        {
            IEnumerable<FishCatch> scopedCatches = catches;

            if (scoreCategory == FishingTournamentScoreCategory.SpecificFish && targetFishTypeId.HasValue)
            {
                scopedCatches = scopedCatches.Where(c => c.FishTypeId == targetFishTypeId.Value);
            }

            var grouped = scopedCatches
                .GroupBy(c => new { c.UserId, c.Username })
                .Select(group => new TournamentStanding
                {
                    UserId = group.Key.UserId,
                    Username = group.Key.Username,
                    CatchCount = group.Count(),
                    LastCaughtAtUtc = group.Max(c => c.CaughtAt),
                    Score = scoreCategory switch
                    {
                        FishingTournamentScoreCategory.Largest or FishingTournamentScoreCategory.SpecificFish => group.Max(c => c.Weight),
                        FishingTournamentScoreCategory.MostValuable => group.Max(c => c.GoldEarned),
                        FishingTournamentScoreCategory.Smallest => group.Min(c => c.Weight),
                        FishingTournamentScoreCategory.Average => group.Average(c => c.Weight),
                        FishingTournamentScoreCategory.MostCatches => group.Count(),
                        FishingTournamentScoreCategory.TotalWeight => group.Sum(c => c.Weight),
                        _ => 0
                    },
                    TotalStars = group.Sum(c => c.Stars)
                });

            return scoreCategory == FishingTournamentScoreCategory.Smallest
                ? [.. grouped.OrderBy(x => x.Score).ThenBy(x  => x.CatchCount).ThenBy(x => x.TotalStars)]
                : [.. grouped.OrderByDescending(x => x.Score).ThenBy(x => x.CatchCount).ThenBy(x => x.TotalStars)];
        }

        private static async Task<List<FishCatch>> GetTournamentCatches(ApplicationDbContext context, FishingTournament tournament, DateTime? settlementEndUtc, bool useLinkedCatchesOnly)
        {
            var linkedCatchIds = await context.FishingTournamentCatches
                .AsNoTracking()
                .Where(link => link.FishingTournamentId == tournament.Id)
                .Select(link => link.FishCatchId)
                .ToListAsync();

            if (linkedCatchIds.Count > 0)
            {
                return await context.FishCatches
                    .AsNoTracking()
                    .Include(c => c.FishType)
                    .Where(c => linkedCatchIds.Contains(c.Id))
                    .ToListAsync();
            }

            if (useLinkedCatchesOnly)
            {
                return [];
            }

            var startUtc = tournament.StartsAtUtc ?? DateTime.MinValue;
            var endUtc = settlementEndUtc ?? tournament.EndsAtUtc ?? DateTime.UtcNow;
            var eligibleFishTypeIds = tournament.EligibleFish.Select(e => e.FishTypeId).ToHashSet();

            var query = context.FishCatches
                .AsNoTracking()
                .Include(c => c.FishType)
                .Where(c => c.CaughtAt >= startUtc && c.CaughtAt <= endUtc);

            if (eligibleFishTypeIds.Count > 0)
            {
                query = query.Where(c => eligibleFishTypeIds.Contains(c.FishTypeId));
            }

            return await query.ToListAsync();
        }

        private sealed class TournamentStanding
        {
            public string UserId { get; set; } = string.Empty;
            public string Username { get; set; } = string.Empty;
            public int CatchCount { get; set; }
            public DateTime? LastCaughtAtUtc { get; set; }
            public int TotalStars { get; set; }
            public double Score { get; set; }
        }

        private sealed class TournamentRewardWinner
        {
            public string UserId { get; set; } = string.Empty;
            public string Username { get; set; } = string.Empty;
            public int Placement { get; set; }
            public int PointTypeId { get; set; }
            public string PointTypeName { get; set; } = string.Empty;
            public FishingTournamentScoreCategory ScoreCategory { get; set; }
            public FishingTournamentRewardKind RewardKind { get; set; }
            public long RewardAmount { get; set; }
        }

        public async Task DeleteFishingTournament(int id)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var tournament = await context.FishingTournaments.FindAsync(id);
            if (tournament == null)
            {
                return;
            }

            context.FishingTournaments.Remove(tournament);
            await context.SaveChangesAsync();
        }

        #endregion

        #region Gold Management

        public async Task<FishingGold?> GetUserGold(string userId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await context.FishingGolds.FirstOrDefaultAsync(g => g.UserId == userId);
        }

        public async Task AddGoldToUser(string userId, string username, int amount)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var gold = await context.FishingGolds.FirstOrDefaultAsync(g => g.UserId == userId);
            if (gold == null)
            {
                gold = new FishingGold { UserId = userId, Username = username, TotalGold = amount };
                context.FishingGolds.Add(gold);
            }
            else
            {
                gold.TotalGold += amount;
                gold.Username = username;
            }
            await context.SaveChangesAsync();
        }

        public async Task RemoveGoldFromUser(string userId, int amount)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var gold = await context.FishingGolds.FirstOrDefaultAsync(g => g.UserId == userId);
            if (gold != null && gold.TotalGold >= amount)
            {
                gold.TotalGold -= amount;
                await context.SaveChangesAsync();
            }
        }

        public async Task SetUserGold(string userId, string username, int amount)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var gold = await context.FishingGolds.FirstOrDefaultAsync(g => g.UserId == userId);
            if (gold == null)
            {
                gold = new FishingGold { UserId = userId, Username = username, TotalGold = amount };
                context.FishingGolds.Add(gold);
            }
            else
            {
                gold.TotalGold = amount;
                gold.Username = username;
            }
            await context.SaveChangesAsync();
        }

        public async Task<List<FishingGold>> GetAllPlayersWithGold()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await context.FishingGolds
                .OrderBy(g => g.Username)
                .ToListAsync();
        }

        #endregion

        #region Settings

        public async Task<FishingSettings?> GetSettings()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var settings = await context.FishingSettings.SingleOrDefaultAsync();
            if (settings == null)
            {
                settings = new FishingSettings();
                context.FishingSettings.Add(settings);
                await context.SaveChangesAsync();
            }
            return settings;
        }

        public async Task UpdateSettings(FishingSettings settings)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.FishingSettings.Update(settings);
            await context.SaveChangesAsync();
        }

        #endregion

        #region Admin Operations

        public async Task ResetAllUserData()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Remove all user catches
            await context.FishCatches.ExecuteDeleteAsync();

            // Remove all user gold records
            await context.FishingGolds.ExecuteDeleteAsync();

            // Remove all user boosts (purchased items)
            await context.UserFishingBoosts.ExecuteDeleteAsync();

            // Remove all user snap history records
            await context.FishingSnapEvents.ExecuteDeleteAsync();

            await context.SaveChangesAsync();
        }

        public async Task<int> SyncAllFishRarities()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var settings = await GetSettings();
            if (settings == null)
            {
                throw new InvalidOperationException("Fishing settings not found");
            }

            var allFish = await context.FishTypes.ToListAsync();
            var updateCount = 0;

            foreach (var fish in allFish)
            {
                var oldRarity = fish.Rarity;
                var newRarity = CalculateRarityFromGold(fish.BaseGold, settings);

                if (oldRarity != newRarity)
                {
                    fish.Rarity = newRarity;
                    updateCount++;
                }
            }

            if (updateCount > 0)
            {
                await context.SaveChangesAsync();
            }

            return updateCount;
        }

        private FishRarity CalculateRarityFromGold(int baseGold, FishingSettings settings)
        {
            return baseGold switch
            {
                var gold when gold >= settings.RarityLegendaryThreshold => FishRarity.Legendary,
                var gold when gold >= settings.RarityEpicThreshold => FishRarity.Epic,
                var gold when gold >= settings.RarityRareThreshold => FishRarity.Rare,
                var gold when gold >= settings.RarityUncommonThreshold => FishRarity.Uncommon,
                _ => FishRarity.Common
            };
        }

        #endregion
    }
}
