using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
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
            // Only get enabled fish types
            var allFishTypes = await _fishingService.GetAllFishTypes();
            var fishTypes = allFishTypes.Where(f => f.Enabled).ToList();

            if (fishTypes.Count == 0)
            {
                throw new InvalidOperationException("No enabled fish types available");
            }

            var settings = await _fishingService.GetSettings();
            // Only get equipped items, not all boosts
            var userBoosts = await _inventoryService.GetUserEquippedItems(userId);

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
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

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

            context.FishCatches.Add(fishCatch);
            await context.SaveChangesAsync();

            fishCatch.FishType = fishType;

            var activeTournaments = await context.FishingTournaments
                .AsNoTracking()
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
                        context.FishingTournamentCatches.Add(new FishingTournamentCatch
                        {
                            FishingTournamentId = tournament.Id,
                            FishCatchId = fishCatch.Id
                        });
                    }
                }
                await context.SaveChangesAsync();
            }

            await _fishingService.AddGoldToUser(userId, username, gold);

            // Consume uses from equipped items
            foreach (var boost in userBoosts)
            {
                await _inventoryService.ConsumeItemUse(userId, boost.Id);
            }

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
