using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
using PenguinTwitchBot.Database.Repository;
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
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.FishTypes.GetAsync(orderBy: q => q.OrderBy(f => f.Name), includeProperties: "Categories");
        }

        public async Task<List<FishType>> GetFishTypesWithCatches()
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var catchQuery = db.FishCatches.Query();
            return await db.FishTypes
                .Find(f => f.Enabled && catchQuery.Any(c => c.FishTypeId == f.Id))
                .OrderBy(f => f.Name)
                .ToListAsync();
        }

        public async Task<FishType?> GetFishTypeById(int id)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.FishTypes.GetByIdAsync(id);
        }

        public async Task AddFishType(FishType fishType)
        {
            FishingValueRules.NormalizeAndValidate(fishType);
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.FishTypes.Add(fishType);
            await db.SaveChangesAsync();
        }

        public async Task UpdateFishType(FishType fishType)
        {
            FishingValueRules.NormalizeAndValidate(fishType);
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var existing = await db.FishTypes
                .Find(f => f.Id == fishType.Id)
                .Include(f => f.Categories)
                .FirstOrDefaultAsync();

            if (existing == null)
            {
                db.FishTypes.Add(fishType);
                await db.SaveChangesAsync();
                return;
            }

            existing.Name = fishType.Name;
            existing.Rarity = fishType.Rarity;
            existing.BaseWeight = fishType.BaseWeight;
            existing.BaseGold = fishType.BaseGold;
            existing.ImageFileName = fishType.ImageFileName;
            existing.Enabled = fishType.Enabled;

            db.FishCategories.RemoveRange(existing.Categories);
            existing.Categories = fishType.Categories
                .Select(category => new FishCategory { Category = category.Category })
                .ToList();

            await db.SaveChangesAsync();
        }

        public async Task DeleteFishType(int id)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var fishType = await db.FishTypes.GetByIdAsync(id);
            if (fishType != null)
            {
                db.FishTypes.Remove(fishType);
                await db.SaveChangesAsync();
            }
        }

        #endregion

        #region Fish Catch Queries

        public async Task<List<FishCatch>> GetTopCatchesForFishType(int fishTypeId, int count = 10)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.FishCatches
                .Find(c => c.FishTypeId == fishTypeId)
                .Include(c => c.FishType)
                .OrderByDescending(c => c.Stars)
                .ThenByDescending(c => c.Weight)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<FishCatch>> GetUserCatches(string userId, int count = 50)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.FishCatches
                .Find(c => c.UserId == userId)
                .Include(c => c.FishType)
                .OrderByDescending(c => c.CaughtAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<FishCatch?> GetUserBestCatchForFishType(string userId, int fishTypeId)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.FishCatches
                .Find(c => c.UserId == userId && c.FishTypeId == fishTypeId)
                .Include(c => c.FishType)
                .OrderByDescending(c => c.Stars)
                .ThenByDescending(c => c.Weight)
                .FirstOrDefaultAsync();
        }

        public async Task<int> GetUserCatchCountForFishType(string userId, int fishTypeId)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.FishCatches
                .Find(c => c.UserId == userId && c.FishTypeId == fishTypeId)
                .CountAsync();
        }

        public async Task<Dictionary<int, FishCatch>> GetUserBestCatchesForAllFishTypes(string userId)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var bestCatches = await db.FishCatches
                .Find(c => c.UserId == userId)
                .Include(c => c.FishType)
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
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var counts = await db.FishCatches
                .Find(c => c.UserId == userId)
                .GroupBy(c => c.FishTypeId)
                .Select(g => new { FishTypeId = g.Key, Count = g.Count() })
                .ToListAsync();

            return counts.ToDictionary(c => c.FishTypeId, c => c.Count);
        }

        public async Task<List<FishingTournament>> GetAllFishingTournaments(int count = 100)
        {
            count = Math.Max(1, Math.Min(count, 500));

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            return await db.FishingTournaments.Query()
                .AsNoTracking()
                .AsSplitQuery()
                .Include(t => t.EntryFeePointType)
                .Include(t => t.EligibleFish)
                    .ThenInclude(e => e.FishType)
                        .ThenInclude(f => f.Categories)
                .Include(t => t.EligibleCategories)
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
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            return await db.FishingTournaments.Query()
                .AsNoTracking()
                .AsSplitQuery()
                .Include(t => t.EntryFeePointType)
                .Include(t => t.EligibleFish)
                    .ThenInclude(e => e.FishType)
                        .ThenInclude(f => f.Categories)
                .Include(t => t.EligibleCategories)
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
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            return await db.FishingTournaments.Query()
                .AsNoTracking()
                .AsSplitQuery()
                .Include(t => t.EntryFeePointType)
                .Include(t => t.EligibleFish)
                    .ThenInclude(e => e.FishType)
                        .ThenInclude(f => f.Categories)
                .Include(t => t.EligibleCategories)
                .Include(t => t.RewardRules)
                    .ThenInclude(r => r.PointType)
                .Include(t => t.RewardRules)
                    .ThenInclude(r => r.TargetFishType)
                .Where(t => t.Status == FishingTournamentStatus.Completed || t.Status == FishingTournamentStatus.Cancelled)
                .OrderByDescending(t => t.EndsAtUtc ?? t.StartsAtUtc)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<FishCatch>> GetRecentCatches(int count = 20)
        {
            count = Math.Max(1, Math.Min(count, 100));

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return await context.FishCatches
                .Include(c => c.FishType)
                .OrderByDescending(c => c.CaughtAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<FishingTournament?> GetFishingTournamentById(int id)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            return await db.FishingTournaments
                .Find(t => t.Id == id)
                .AsSplitQuery()
                .Include(t => t.EntryFeePointType)
                .Include(t => t.EligibleFish)
                    .ThenInclude(e => e.FishType)
                        .ThenInclude(f => f.Categories)
                .Include(t => t.EligibleCategories)
                .Include(t => t.RewardRules)
                    .ThenInclude(r => r.PointType)
                .Include(t => t.RewardRules)
                    .ThenInclude(r => r.TargetFishType)
                .FirstOrDefaultAsync();
        }

        public async Task<List<FishingTournamentStanding>> GetFishingTournamentStandings(int tournamentId, int count = 10)
        {
            count = Math.Max(1, Math.Min(count, 100));

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var tournament = await db.FishingTournaments
                .Find(t => t.Id == tournamentId)
                .AsNoTracking()
                .AsSplitQuery()
                .Include(t => t.EligibleFish)
                    .ThenInclude(e => e.FishType)
                        .ThenInclude(f => f.Categories)
                .Include(t => t.EligibleCategories)
                .FirstOrDefaultAsync();

            if (tournament == null)
            {
                return [];
            }

            var catches = await GetTournamentCatches(db, tournament, null, useLinkedCatchesOnly: true);
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

        public async Task<Dictionary<int, FishingTournamentRewardStanding>> GetFishingTournamentRewardStandings(int tournamentId)
        {
            var results = new Dictionary<int, FishingTournamentRewardStanding>();

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var tournament = await db.FishingTournaments
                .Find(t => t.Id == tournamentId)
                .AsNoTracking()
                .AsSplitQuery()
                .Include(t => t.EligibleFish)
                    .ThenInclude(e => e.FishType)
                        .ThenInclude(f => f.Categories)
                .Include(t => t.EligibleCategories)
                .Include(t => t.RewardRules)
                .FirstOrDefaultAsync();

            if (tournament == null || tournament.RewardRules.Count == 0)
            {
                return results;
            }

            // Mirrors settlement: a completed tournament has EndsAtUtc set, so the window matches what was awarded.
            var catches = await GetTournamentCatches(db, tournament, null, useLinkedCatchesOnly: false);
            if (catches.Count == 0)
            {
                return results;
            }

            foreach (var rewardRule in tournament.RewardRules.Where(rule => rule.Enabled))
            {
                var winner = CalculateStandings(catches, rewardRule)
                    .Take(rewardRule.Placement)
                    .LastOrDefault();

                if (winner == null)
                {
                    continue;
                }

                results[rewardRule.Id] = new FishingTournamentRewardStanding
                {
                    RewardRuleId = rewardRule.Id,
                    UserId = winner.UserId,
                    Username = winner.Username,
                    Score = winner.Score,
                    CatchCount = winner.CatchCount
                };
            }

            return results;
        }

        public async Task<FishingTournament?> StartFishingTournament(int id)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var tournament = await db.FishingTournaments
                .Find(t => t.Id == id)
                .AsSplitQuery()
                .Include(t => t.EntryFeePointType)
                .Include(t => t.EligibleFish)
                    .ThenInclude(e => e.FishType)
                .Include(t => t.RewardRules)
                    .ThenInclude(r => r.PointType)
                .Include(t => t.RewardRules)
                    .ThenInclude(r => r.TargetFishType)
                .FirstOrDefaultAsync();

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

            await db.SaveChangesAsync();

            if (!wasActive)
            {
                await TriggerFishingTournamentLifecycleActionsAsync(tournament, TriggerTypes.FishingTournamentStart, FishingTournamentStartTriggerName);
            }

            return tournament;
        }

        public async Task<FishingTournament?> CloneAndStartFishingTournament(int templateTournamentId)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var template = await db.FishingTournaments
                .Find(t => t.Id == templateTournamentId)
                .AsNoTracking()
                .AsSplitQuery()
                .Include(t => t.EligibleFish)
                .Include(t => t.EligibleCategories)
                .Include(t => t.RewardRules)
                .FirstOrDefaultAsync();

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
                EligibleCategories = template.EligibleCategories
                    .Select(category => new FishingTournamentEligibleCategory { Category = category.Category })
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
                        GoldAmount = rule.GoldAmount,
                        Enabled = rule.Enabled
                    })
                    .ToList()
            };

            db.FishingTournaments.Add(clonedTournament);
            await db.SaveChangesAsync();

            return await StartFishingTournament(clonedTournament.Id);
        }

        public async Task<FishingTournament?> ReopenFishingTournament(int id)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var tournament = await db.FishingTournaments
                .Find(t => t.Id == id)
                .AsSplitQuery()
                .Include(t => t.EntryFeePointType)
                .Include(t => t.EligibleFish)
                    .ThenInclude(e => e.FishType)
                .Include(t => t.RewardRules)
                    .ThenInclude(r => r.PointType)
                .Include(t => t.RewardRules)
                    .ThenInclude(r => r.TargetFishType)
                .FirstOrDefaultAsync();

            if (tournament == null)
            {
                return null;
            }

            if (tournament.Status is not (FishingTournamentStatus.Completed or FishingTournamentStatus.Cancelled))
            {
                return tournament;
            }

            var linkedCatches = await db.FishingTournamentCatches.GetAsync(link => link.FishingTournamentId == id);

            if (linkedCatches.Count > 0)
            {
                db.FishingTournamentCatches.RemoveRange(linkedCatches);
            }

            tournament.Enabled = true;
            tournament.Status = FishingTournamentStatus.Scheduled;
            tournament.StartsAtUtc = null;
            tournament.EndsAtUtc = null;

            await db.SaveChangesAsync();
            return tournament;
        }

        public async Task<FishingTournament> SaveFishingTournament(FishingTournament tournament)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var persistedTournament = await db.FishingTournaments
                .Find(t => t.Id == tournament.Id)
                .AsSplitQuery()
                .Include(t => t.EligibleFish)
                .Include(t => t.EligibleCategories)
                .Include(t => t.RewardRules)
                .FirstOrDefaultAsync();

            if (persistedTournament == null)
            {
                db.FishingTournaments.Add(tournament);
                await db.SaveChangesAsync();
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

            db.FishingTournamentFishTypes.RemoveRange(persistedTournament.EligibleFish);
            db.FishingTournamentRewardRules.RemoveRange(persistedTournament.RewardRules);
            db.FishingTournamentEligibleCategories.RemoveRange(persistedTournament.EligibleCategories);

            persistedTournament.EligibleFish = tournament.EligibleFish
                .Select(fish => new FishingTournamentFishType
                {
                    FishTypeId = fish.FishTypeId
                })
                .ToList();

            persistedTournament.EligibleCategories = tournament.EligibleCategories
                .Select(category => new FishingTournamentEligibleCategory
                {
                    Category = category.Category
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
                    GoldAmount = rule.GoldAmount,
                    Enabled = rule.Enabled
                })
                .ToList();

            await db.SaveChangesAsync();
            return persistedTournament;
        }

        public async Task<FishingTournament?> EndFishingTournament(int id)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var tournament = await db.FishingTournaments
                .Find(t => t.Id == id)
                .AsSplitQuery()
                .Include(t => t.EntryFeePointType)
                .Include(t => t.EligibleFish)
                    .ThenInclude(e => e.FishType)
                .Include(t => t.EligibleCategories)
                .Include(t => t.RewardRules)
                    .ThenInclude(r => r.PointType)
                .Include(t => t.RewardRules)
                    .ThenInclude(r => r.TargetFishType)
                .FirstOrDefaultAsync();

            if (tournament == null)
            {
                return null;
            }

            // Only settle once: skip if already Completed or Cancelled.
            if (tournament.Status is FishingTournamentStatus.Completed or FishingTournamentStatus.Cancelled)
            {
                return tournament;
            }

            // Gold credits and the completion flip share this db/SaveChangesAsync so a failed save can't leave gold
            // credited against a tournament that a retry would settle again.
            var rewardWinners = await SettleFishingTournamentRewards(db, tournament, DateTime.UtcNow);

            tournament.Status = FishingTournamentStatus.Completed;
            tournament.Enabled = false;
            tournament.EndsAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync();

            await TriggerFishingTournamentLifecycleActionsAsync(
                tournament,
                TriggerTypes.FishingTournamentEnd,
                FishingTournamentEndTriggerName,
                rewardWinners);

            return tournament;
        }

        private async Task<List<TournamentRewardWinner>> SettleFishingTournamentRewards(IUnitOfWork db, FishingTournament tournament, DateTime settlementEndUtc)
        {
            var winners = new List<TournamentRewardWinner>();

            if (tournament.RewardRules.Count == 0)
            {
                return winners;
            }

            var catches = await GetTournamentCatches(db, tournament, settlementEndUtc, useLinkedCatchesOnly: false);

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

                var goldAmount = Math.Max(0L, rewardRule.GoldAmount ?? 0L);

                if (rewardAmount <= 0 && goldAmount <= 0)
                {
                    continue;
                }

                if (rewardAmount > 0)
                {
                    await _pointsSystem.AddPointsByUserId(winner.UserId, rewardRule.PointTypeId, rewardAmount);
                }

                if (goldAmount > 0)
                {
                    var gold = await db.FishingGolds.Find(g => g.UserId == winner.UserId).FirstOrDefaultAsync();
                    var existingTotal = gold?.TotalGold ?? 0;

                    // TotalGold is a 32-bit column; clamp the sum (not just the reward) so the persisted
                    // balance and the reported/logged amount never diverge or silently wrap.
                    var clampedTotal = Math.Clamp(existingTotal + goldAmount, 0L, int.MaxValue);
                    goldAmount = clampedTotal - existingTotal;

                    if (gold == null)
                    {
                        db.FishingGolds.Add(new FishingGold { UserId = winner.UserId, Username = winner.Username, TotalGold = (int)clampedTotal });
                    }
                    else
                    {
                        gold.TotalGold = (int)clampedTotal;
                        gold.Username = winner.Username;
                    }
                }

                winners.Add(new TournamentRewardWinner
                {
                    UserId = winner.UserId,
                    Username = winner.Username,
                    Placement = rewardRule.Placement,
                    PointTypeId = rewardRule.PointTypeId,
                    PointTypeName = rewardRule.PointType?.Name ?? string.Empty,
                    ScoreCategory = rewardRule.ScoreCategory,
                    RewardKind = rewardRule.RewardKind,
                    RewardAmount = rewardAmount,
                    GoldAmount = goldAmount
                });

                _logger.LogInformation(
                    "Settled tournament {TournamentId} reward for {Username}: placement {Placement}, category {Category}, amount {Amount} on point type {PointTypeId}, gold {Gold}",
                    tournament.Id,
                    winner.Username,
                    rewardRule.Placement,
                    rewardRule.ScoreCategory,
                    rewardAmount,
                    rewardRule.PointTypeId,
                    goldAmount);
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

                var actions = await actionManagement.GetActionsByTriggerTypeAndNameEnabledAsync(triggerType, triggerName);
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
            {
                var parts = new List<string>();
                if (winner.RewardAmount > 0)
                {
                    parts.Add($"{winner.RewardAmount} {(string.IsNullOrWhiteSpace(winner.PointTypeName) ? $"PointType:{winner.PointTypeId}" : winner.PointTypeName)}");
                }
                if (winner.GoldAmount > 0)
                {
                    parts.Add($"{winner.GoldAmount} Gold");
                }
                return $"#{winner.Placement} {winner.Username} won {string.Join(" + ", parts)}";
            }));
        }

        private static List<TournamentStanding> CalculateStandings(List<TournamentCatchEntry> catches, FishingTournamentRewardRule rewardRule)
        {
            return CalculateStandings(catches, rewardRule.ScoreCategory, rewardRule.TargetFishTypeId);
        }

        private static List<TournamentStanding> CalculateStandings(List<TournamentCatchEntry> catches, FishingTournamentScoreCategory scoreCategory, int? targetFishTypeId = null)
        {
            IEnumerable<TournamentCatchEntry> scopedCatches = catches;

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

        private static async Task<List<TournamentCatchEntry>> GetTournamentCatches(IUnitOfWork db, FishingTournament tournament, DateTime? settlementEndUtc, bool useLinkedCatchesOnly)
        {
            // Recorded tournament catches are self-contained snapshots, so they stay valid
            // even after the source FishCatch rows are purged.
            var recordedCatches = await db.FishingTournamentCatches
                .Find(link => link.FishingTournamentId == tournament.Id)
                .AsNoTracking()
                .Select(link => new TournamentCatchEntry(
                    link.UserId,
                    link.Username,
                    link.FishTypeId,
                    link.Stars,
                    link.Weight,
                    link.GoldEarned,
                    link.CaughtAt))
                .ToListAsync();

            if (recordedCatches.Count > 0)
            {
                return recordedCatches;
            }

            if (useLinkedCatchesOnly)
            {
                return [];
            }

            var startUtc = tournament.StartsAtUtc ?? DateTime.MinValue;
            var endUtc = settlementEndUtc ?? tournament.EndsAtUtc ?? DateTime.UtcNow;

            var hasEligibleFish = tournament.EligibleFish.Count > 0;
            var hasEligibleCategories = tournament.EligibleCategories.Count > 0;

            var query = db.FishCatches
                .Find(c => c.CaughtAt >= startUtc && c.CaughtAt <= endUtc)
                .AsNoTracking();

            // No fish and no categories selected means all fish are eligible (default behavior).
            if (hasEligibleFish || hasEligibleCategories)
            {
                var eligibleFishTypeIds = tournament.EligibleFish.Select(e => e.FishTypeId).ToHashSet();
                var eligibleCategorySet = tournament.EligibleCategories
                    .Select(e => e.Category)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var eligibleByCategoryFishTypeIds = new HashSet<int>();
                if (eligibleCategorySet.Count > 0)
                {
                    eligibleByCategoryFishTypeIds = await db.FishCategories
                        .Find(fc => eligibleCategorySet.Contains(fc.Category))
                        .Select(fc => fc.FishTypeId)
                        .Distinct()
                        .ToHashSetAsync();
                }

                query = query
                    .Where(c =>
                        (hasEligibleFish && eligibleFishTypeIds.Contains(c.FishTypeId)) ||
                        (hasEligibleCategories && eligibleByCategoryFishTypeIds.Contains(c.FishTypeId)));
            }

            return await query
                .Select(c => new TournamentCatchEntry(
                    c.UserId,
                    c.Username,
                    c.FishTypeId,
                    c.Stars,
                    c.Weight,
                    c.GoldEarned,
                    c.CaughtAt))
                .ToListAsync();
        }

        private sealed record TournamentCatchEntry(
            string UserId,
            string Username,
            int FishTypeId,
            int Stars,
            double Weight,
            int GoldEarned,
            DateTime CaughtAt);

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
            public long GoldAmount { get; set; }
        }

        public async Task DeleteFishingTournament(int id)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var tournament = await db.FishingTournaments.GetByIdAsync(id);
            if (tournament == null)
            {
                return;
            }

            db.FishingTournaments.Remove(tournament);
            await db.SaveChangesAsync();
        }

        #endregion

        #region Gold Management

        public async Task<FishingGold?> GetUserGold(string userId)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.FishingGolds.Find(g => g.UserId == userId).FirstOrDefaultAsync();
        }

        public async Task AddGoldToUser(string userId, string username, int amount)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var gold = await db.FishingGolds.Find(g => g.UserId == userId).FirstOrDefaultAsync();
            if (gold == null)
            {
                gold = new FishingGold { UserId = userId, Username = username, TotalGold = amount };
                db.FishingGolds.Add(gold);
            }
            else
            {
                gold.TotalGold += amount;
                gold.Username = username;
            }
            await db.SaveChangesAsync();
        }

        public async Task RemoveGoldFromUser(string userId, int amount)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var gold = await db.FishingGolds.Find(g => g.UserId == userId).FirstOrDefaultAsync();
            if (gold != null && gold.TotalGold >= amount)
            {
                gold.TotalGold -= amount;
                await db.SaveChangesAsync();
            }
        }

        public async Task SetUserGold(string userId, string username, int amount)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var gold = await db.FishingGolds.Find(g => g.UserId == userId).FirstOrDefaultAsync();
            if (gold == null)
            {
                gold = new FishingGold { UserId = userId, Username = username, TotalGold = amount };
                db.FishingGolds.Add(gold);
            }
            else
            {
                gold.TotalGold = amount;
                gold.Username = username;
            }
            await db.SaveChangesAsync();
        }

        public async Task<List<FishingGold>> GetAllPlayersWithGold()
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.FishingGolds.GetAsync(orderBy: q => q.OrderBy(g => g.Username));
        }

        #endregion

        #region Settings

        public async Task<FishingSettings?> GetSettings()
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var settings = await db.FishingSettings.Query().SingleOrDefaultAsync();
            if (settings == null)
            {
                settings = new FishingSettings();
                db.FishingSettings.Add(settings);
                await db.SaveChangesAsync();
            }
            return settings;
        }

        public async Task UpdateSettings(FishingSettings settings)
        {
            if (!FishingRarityThresholdRules.TryValidateThresholdOrder(settings, out var thresholdValidationMessage))
            {
                throw new InvalidOperationException(thresholdValidationMessage);
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.FishingSettings.Update(settings);
            await db.SaveChangesAsync();
        }

        #endregion

        #region Admin Operations

        public async Task ResetAllUserData()
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            // FishingTournamentCatches keep their own snapshot of the catch data, so tournament
            // history survives this (the FK is set to null by the database).
            await db.FishCatches.ExecuteDeleteAllAsync();

            // Remove all user gold records
            await db.FishingGolds.ExecuteDeleteAllAsync();

            // Remove all user boosts (purchased items)
            await db.UserFishingBoosts.ExecuteDeleteAllAsync();

            // Remove all user snap history records
            await db.FishingSnapEvents.ExecuteDeleteAllAsync();
        }

        public async Task<int> SyncAllFishRarities()
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var settings = await GetSettings();
            if (settings == null)
            {
                throw new InvalidOperationException("Fishing settings not found");
            }

            var allFish = (await db.FishTypes.GetAllAsync()).ToList();
            var updateCount = 0;

            foreach (var fish in allFish)
            {
                var oldRarity = fish.Rarity;
                var newRarity = FishingRarityThresholdRules.CalculateRarityFromGold(fish.BaseGold, settings);

                if (oldRarity != newRarity)
                {
                    fish.Rarity = newRarity;
                    updateCount++;
                }
            }

            if (updateCount > 0)
            {
                await db.SaveChangesAsync();
            }

            return updateCount;
        }

        public async Task<int> CleanOrphanedTournamentCategories()
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var categoryQuery = db.FishCategories.Query();
            var orphaned = await db.FishingTournamentEligibleCategories
                .Find(ec => !categoryQuery.Any(fc => fc.Category == ec.Category))
                .ToListAsync();

            if (orphaned.Count > 0)
            {
                db.FishingTournamentEligibleCategories.RemoveRange(orphaned);
                await db.SaveChangesAsync();
            }

            return orphaned.Count;
        }

        private FishRarity CalculateRarityFromGold(int baseGold, FishingSettings settings)
        {
            return FishingRarityThresholdRules.CalculateRarityFromGold(baseGold, settings);
        }

        #endregion
    }
}
