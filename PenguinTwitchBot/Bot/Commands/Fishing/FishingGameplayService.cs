using PenguinTwitchBot.Database.Bot.Models.Fishing;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Bot.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    public class FishingGameplayService : IFishingGameplayService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<FishingGameplayService> _logger;
        private readonly IHubContext<MainHub> _hubContext;
        private readonly IFishingService _fishingService;
        private readonly IFishingInventoryService _inventoryService;

        public FishingGameplayService(
            IServiceScopeFactory scopeFactory, 
            ILogger<FishingGameplayService> logger,
            IHubContext<MainHub> hubContext,
            IFishingService fishingService,
            IFishingInventoryService inventoryService)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _hubContext = hubContext;
            _fishingService = fishingService;
            _inventoryService = inventoryService;
        }

        public async Task<FishingAttemptResult> PerformFishingAttempt(string userId, string username)
        {
            // These reads are independent of each other, so fetch them concurrently.
            var fishTypesTask = _fishingService.GetAllFishTypes();
            var settingsTask = _fishingService.GetSettings();
            var userBoostsTask = _inventoryService.GetUserEquippedItems(userId);
            await Task.WhenAll(fishTypesTask, settingsTask, userBoostsTask);

            // Only get enabled fish types
            var fishTypes = fishTypesTask.Result.Where(f => f.Enabled).ToList();

            if (fishTypes.Count == 0)
            {
                throw new InvalidOperationException("No enabled fish types available");
            }

            var settings = settingsTask.Result;
            var userBoosts = userBoostsTask.Result;

            var lineSnapChance = settings != null &&
                !double.IsNaN(settings.LineSnapChance) &&
                !double.IsInfinity(settings.LineSnapChance) &&
                settings.LineSnapChance >= 0 &&
                settings.LineSnapChance <= 1
                ? settings.LineSnapChance
                : FishingSettings.DefaultLineSnapChance;
            var rodSnapChance = settings != null &&
                !double.IsNaN(settings.RodSnapChance) &&
                !double.IsInfinity(settings.RodSnapChance) &&
                settings.RodSnapChance >= 0 &&
                settings.RodSnapChance <= 1
                ? settings.RodSnapChance
                : FishingSettings.DefaultRodSnapChance;

            if (StaticTools.NextDouble() < rodSnapChance)
            {
                await _inventoryService.ConsumeItemsOnRodSnap(userId, username);

                try
                {
                    await _hubContext.Clients.All.SendAsync("ReceiveFishCatch", new
                    {
                        Id = 0,
                        UserId = userId,
                        Username = username,
                        FishTypeId = 0,
                        FishName = "ROD SNAPPED",
                        FishRarity = "Accident",
                        FishImageFileName = "",
                        Stars = 0,
                        Weight = 0.0,
                        GoldEarned = 0,
                        CaughtAt = DateTime.UtcNow,
                        IsLineSnapped = true,
                        IsRodSnapped = true
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to broadcast snapped rod event via SignalR");
                }

                return new FishingAttemptResult
                {
                    Outcome = FishingAttemptOutcome.RodSnapped,
                    LostEquipmentSlots = new List<EquipmentSlot>
                    {
                        EquipmentSlot.Rod,
                        EquipmentSlot.Line,
                        EquipmentSlot.Hook
                    }
                };
            }

            if (StaticTools.NextDouble() < lineSnapChance)
            {
                await _inventoryService.ConsumeItemsOnLineSnap(userId, username);

                try
                {
                    await _hubContext.Clients.All.SendAsync("ReceiveFishCatch", new
                    {
                        Id = 0,
                        UserId = userId,
                        Username = username,
                        FishTypeId = 0,
                        FishName = "LINE SNAPPED",
                        FishRarity = "Accident",
                        FishImageFileName = "",
                        Stars = 0,
                        Weight = 0.0,
                        GoldEarned = 0,
                        CaughtAt = DateTime.UtcNow,
                        IsLineSnapped = true
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to broadcast snapped line event via SignalR");
                }

                return new FishingAttemptResult
                {
                    Outcome = FishingAttemptOutcome.LineSnapped,
                    LostEquipmentSlots = new List<EquipmentSlot> { EquipmentSlot.Line, EquipmentSlot.Hook }
                };
            }

            var fishType = FishingCalculations.SelectRandomFish(fishTypes, settings, userBoosts);
            var stars = FishingCalculations.CalculateStars(fishType, userBoosts);
            var weight = FishingCalculations.CalculateWeight(fishType, stars, userBoosts);
            var gold = FishingCalculations.CalculateGold(fishType, stars, weight);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var fishCatch = new FishCatch
            {
                UserId = userId,
                Username = username,
                FishTypeId = fishType.Id,
                Stars = stars,
                Weight = weight,
                GoldEarned = gold,
                CaughtAt = DateTime.UtcNow
            };

            db.FishCatches.Add(fishCatch);
            await db.SaveChangesAsync();

            fishCatch.FishType = fishType;

            var activeTournaments = await db.FishingTournaments.Query()
                .AsNoTracking()
                .AsSplitQuery()
                .Include(t => t.EligibleFish)
                .Include(t => t.EligibleCategories)
                .Where(t => t.Enabled && t.Status == FishingTournamentStatus.Active)
                .Where(t => t.StartsAtUtc == null || t.StartsAtUtc <= fishCatch.CaughtAt)
                .Where(t => t.EndsAtUtc == null || t.EndsAtUtc >= fishCatch.CaughtAt)
                .ToListAsync();

            var activeTournamentIds = activeTournaments.Select(t => t.Id).ToList();
            if(activeTournamentIds.Count > 0)
            {
                var fishCategoryNames = fishType.Categories.Select(c => c.Category).ToList();
                foreach(var tournament in activeTournaments)
                {
                    if (FishingCalculations.IsFishEligible(tournament, fishType.Id, fishCategoryNames))
                    {
                        db.FishingTournamentCatches.Add(new FishingTournamentCatch
                        {
                            FishingTournamentId = tournament.Id,
                            FishCatchId = fishCatch.Id,
                            UserId = fishCatch.UserId,
                            Username = fishCatch.Username,
                            FishTypeId = fishCatch.FishTypeId,
                            Stars = fishCatch.Stars,
                            Weight = fishCatch.Weight,
                            GoldEarned = fishCatch.GoldEarned,
                            CaughtAt = fishCatch.CaughtAt
                        });
                    }
                }
                await db.SaveChangesAsync();
            }

            await _fishingService.AddGoldToUser(userId, username, gold);

            // Consume uses from all equipped items in a single batched update
            await _inventoryService.ConsumeItemUses(userId, userBoosts.Select(b => b.Id));

            // Broadcast the new catch to all connected clients via SignalR
            try
            {
                await _hubContext.Clients.All.SendAsync("ReceiveFishCatch", new
                {
                    fishCatch.Id,
                    fishCatch.UserId,
                    fishCatch.Username,
                    fishCatch.FishTypeId,
                    FishName = fishType.Name,
                    FishRarity = fishType.Rarity.ToString(),
                    FishImageFileName = fishType.ImageFileName,
                    fishCatch.Stars,
                    fishCatch.Weight,
                    fishCatch.GoldEarned,
                    fishCatch.CaughtAt
                });
            }
            catch (Exception ex)
            {
                // Log but don't fail the fishing attempt if SignalR broadcast fails
                _logger.LogError(ex, "Failed to broadcast fish catch via SignalR");
            }

            return new FishingAttemptResult
            {
                Outcome = FishingAttemptOutcome.CaughtFish,
                FishCatch = fishCatch
            };
        }
    }
}
