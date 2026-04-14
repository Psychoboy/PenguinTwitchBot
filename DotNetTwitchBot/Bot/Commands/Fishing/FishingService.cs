using DotNetTwitchBot.Bot.Core.Database;
using DotNetTwitchBot.Bot.Models.Fishing;
using DotNetTwitchBot.Extensions;
using DotNetTwitchBot.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace DotNetTwitchBot.Bot.Commands.Fishing
{
    public class FishingService : IFishingService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<FishingService> _logger;

        public FishingService(IServiceScopeFactory scopeFactory, ILogger<FishingService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task<List<FishType>> GetAllFishTypes()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await context.FishTypes.ToListAsync();
        }

        public async Task<List<FishType>> GetFishTypesWithCatches()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await context.FishTypes
                .Where(f => f.Enabled && context.FishCatches.Any(c => c.FishTypeId == f.Id))
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

        public async Task<List<FishingShopItem>> GetAllShopItems()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await context.FishingShopItems
                .Include(s => s.TargetFishType)
                .ToListAsync();
        }

        public async Task<FishingShopItem?> GetShopItemById(int id)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await context.FishingShopItems
                .Include(s => s.TargetFishType)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task AddShopItem(FishingShopItem item)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.FishingShopItems.Add(item);
            await context.SaveChangesAsync();
        }

        public async Task UpdateShopItem(FishingShopItem item)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.FishingShopItems.Update(item);
            await context.SaveChangesAsync();
        }

        public async Task DeleteShopItem(int id)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await context.FishingShopItems.FindAsync(id);
            if (item != null)
            {
                context.FishingShopItems.Remove(item);
                await context.SaveChangesAsync();
            }
        }

        public async Task<List<UserFishingBoost>> GetUserBoosts(string userId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await context.UserFishingBoosts
                .Include(b => b.ShopItem)
                .ThenInclude(s => s!.TargetFishType)
                .Where(b => b.UserId == userId)
                .ToListAsync();
        }

        public async Task<List<UserFishingBoost>> GetUserEquippedItems(string userId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await context.UserFishingBoosts
                .Include(b => b.ShopItem)
                .ThenInclude(s => s!.TargetFishType)
                .Where(b => b.UserId == userId && b.IsEquipped)
                .ToListAsync();
        }

        public async Task<Dictionary<EquipmentSlot, UserFishingBoost>> GetUserEquipmentBySlot(string userId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var equipped = await context.UserFishingBoosts
                .Include(b => b.ShopItem)
                .ThenInclude(s => s!.TargetFishType)
                .Where(b => b.UserId == userId && b.IsEquipped && b.ShopItem!.EquipmentSlot != null)
                .ToListAsync();

            return equipped.Where(e => e.ShopItem?.EquipmentSlot != null)
                          .ToDictionary(e => e.ShopItem!.EquipmentSlot!.Value, e => e);
        }

        public async Task PurchaseBoost(string userId, int shopItemId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var shopItem = await context.FishingShopItems.FindAsync(shopItemId);
            if (shopItem == null || !shopItem.Enabled)
            {
                throw new InvalidOperationException("Shop item not found or disabled");
            }

            var gold = await context.FishingGolds.FirstOrDefaultAsync(g => g.UserId == userId);
            if (gold == null || gold.TotalGold < shopItem.Cost)
            {
                throw new InvalidOperationException("Not enough gold");
            }

            gold.TotalGold -= shopItem.Cost;

            var userBoost = new UserFishingBoost
            {
                UserId = userId,
                ShopItemId = shopItemId,
                RemainingUses = shopItem.MaxUses ?? -1 // -1 means unlimited
            };

            context.UserFishingBoosts.Add(userBoost);

            await context.SaveChangesAsync();
        }

        public async Task EquipItem(string userId, int userBoostId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var userBoost = await context.UserFishingBoosts
                .Include(b => b.ShopItem)
                .FirstOrDefaultAsync(b => b.Id == userBoostId && b.UserId == userId);

            if (userBoost == null)
            {
                throw new InvalidOperationException("Item not found");
            }

            // Check if item has expired (consumable items with 0 uses)
            if (userBoost.ShopItem!.IsConsumable && userBoost.RemainingUses == 0)
            {
                throw new InvalidOperationException("Item has no remaining uses");
            }

            // If item has a slot, unequip any item in that slot
            if (userBoost.ShopItem.EquipmentSlot.HasValue)
            {
                var slotItems = await context.UserFishingBoosts
                    .Include(b => b.ShopItem)
                    .Where(b => b.UserId == userId && 
                               b.IsEquipped && 
                               b.ShopItem!.EquipmentSlot == userBoost.ShopItem.EquipmentSlot)
                    .ToListAsync();

                foreach (var item in slotItems)
                {
                    item.IsEquipped = false;
                }
            }

            userBoost.IsEquipped = true;
            await context.SaveChangesAsync();
        }

        public async Task UnequipItem(string userId, int userBoostId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var userBoost = await context.UserFishingBoosts
                .FirstOrDefaultAsync(b => b.Id == userBoostId && b.UserId == userId);

            if (userBoost == null)
            {
                throw new InvalidOperationException("Item not found");
            }

            userBoost.IsEquipped = false;
            await context.SaveChangesAsync();
        }

        public async Task ConsumeItemUse(string userId, int userBoostId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var userBoost = await context.UserFishingBoosts
                .Include(b => b.ShopItem)
                .FirstOrDefaultAsync(b => b.Id == userBoostId && b.UserId == userId);

            if (userBoost == null || !userBoost.IsEquipped)
            {
                return;
            }

            // Skip if unlimited uses
            if (userBoost.RemainingUses == -1)
            {
                userBoost.LastUsedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();
                return;
            }

            // Only decrement if we have uses remaining (prevent going below 0)
            if (userBoost.RemainingUses > 0)
            {
                userBoost.RemainingUses--;
            }
            userBoost.LastUsedAt = DateTime.UtcNow;

            // If consumable and no uses left, remove the item
            if (userBoost.ShopItem!.IsConsumable && userBoost.RemainingUses <= 0)
            {
                userBoost.IsEquipped = false;
                // Optionally delete the item entirely
                context.UserFishingBoosts.Remove(userBoost);
            }
            else if (userBoost.RemainingUses <= 0)
            {
                // Non-consumable items just get unequipped when out of uses
                userBoost.IsEquipped = false;
            }

            await context.SaveChangesAsync();
        }

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

        public async Task<FishCatch> PerformFishingAttempt(string userId, string username)
        {
            // Only get enabled fish types
            var allFishTypes = await GetAllFishTypes();
            var fishTypes = allFishTypes.Where(f => f.Enabled).ToList();

            if (fishTypes.Count == 0)
            {
                throw new InvalidOperationException("No enabled fish types available");
            }

            var settings = await GetSettings();
            // Only get equipped items, not all boosts
            var userBoosts = await GetUserEquippedItems(userId);

            var fishType = SelectRandomFish(fishTypes, settings, userBoosts);
            var stars = CalculateStars(fishType, userBoosts);
            var weight = CalculateWeight(fishType, stars, userBoosts);
            var gold = CalculateGold(fishType, stars, weight);

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

            await AddGoldToUser(userId, username, gold);

            // Consume uses from equipped items
            foreach (var boost in userBoosts)
            {
                await ConsumeItemUse(userId, boost.Id);
            }

            return fishCatch;
        }

        private FishType SelectRandomFish(List<FishType> fishTypes, FishingSettings? settings, List<UserFishingBoost> boosts)
        {
            var rarityWeights = new Dictionary<FishRarity, double>
            {
                { FishRarity.Common, 50.0 },
                { FishRarity.Uncommon, 30.0 },
                { FishRarity.Rare, 15.0 },
                { FishRarity.Epic, 4.0 },
                { FishRarity.Legendary, 1.0 }
            };

            if (settings?.BoostMode == true)
            {
                var multiplier = settings.BoostModeRarityMultiplier;
                rarityWeights[FishRarity.Uncommon] *= multiplier;
                rarityWeights[FishRarity.Rare] *= multiplier;
                rarityWeights[FishRarity.Epic] *= multiplier;
                rarityWeights[FishRarity.Legendary] *= multiplier;
            }

            foreach (var boost in boosts)
            {
                if (boost.ShopItem?.BoostType == FishingBoostType.GeneralRarityBoost)
                {
                    foreach (var rarity in rarityWeights.Keys.ToList())
                    {
                        if (rarity != FishRarity.Common)
                        {
                            rarityWeights[rarity] *= (1.0 + boost.ShopItem.BoostAmount);
                        }
                    }
                }
                else if (boost.ShopItem?.BoostType == FishingBoostType.SpecificFishBoost && 
                         boost.ShopItem.TargetFishTypeId != null)
                {
                    // Find the target fish and boost its rarity tier
                    var targetFish = fishTypes.FirstOrDefault(f => f.Id == boost.ShopItem.TargetFishTypeId);
                    if (targetFish != null)
                    {
                        // Boost the rarity tier of the target fish
                        rarityWeights[targetFish.Rarity] *= (1.0 + boost.ShopItem.BoostAmount);
                    }
                }

                // Apply secondary boost if present
                if (boost.ShopItem?.BoostType2 == FishingBoostType.GeneralRarityBoost)
                {
                    foreach (var rarity in rarityWeights.Keys.ToList())
                    {
                        if (rarity != FishRarity.Common)
                        {
                            rarityWeights[rarity] *= (1.0 + (boost.ShopItem.BoostAmount2 ?? 0));
                        }
                    }
                }

                // Apply tertiary boost if present
                if (boost.ShopItem?.BoostType3 == FishingBoostType.GeneralRarityBoost)
                {
                    foreach (var rarity in rarityWeights.Keys.ToList())
                    {
                        if (rarity != FishRarity.Common)
                        {
                            rarityWeights[rarity] *= (1.0 + (boost.ShopItem.BoostAmount3 ?? 0));
                        }
                    }
                }
            }

            var totalWeight = rarityWeights.Values.Sum();
            var randomValue = RandomNumberGenerator.GetInt32(0, (int)(totalWeight * 1000)) / 1000.0;
            var currentWeight = 0.0;
            var selectedRarity = FishRarity.Common;

            foreach (var kvp in rarityWeights.OrderBy(k => (int)k.Key))
            {
                currentWeight += kvp.Value;
                if (randomValue < currentWeight)
                {
                    selectedRarity = kvp.Key;
                    break;
                }
            }

            var fishOfRarity = fishTypes.Where(f => f.Rarity == selectedRarity).ToList();
            if (fishOfRarity.Count == 0)
            {
                fishOfRarity = fishTypes;
            }

            var specificBoosts = boosts.Where(b => 
                b.ShopItem?.BoostType == FishingBoostType.SpecificFishBoost && 
                b.ShopItem.TargetFishTypeId != null).ToList();

            if (specificBoosts.Any())
            {
                var weightedFish = new List<(FishType fish, double weight)>();
                foreach (var fish in fishOfRarity)
                {
                    var weight = 1.0;
                    var boost = specificBoosts.FirstOrDefault(b => b.ShopItem?.TargetFishTypeId == fish.Id);
                    if (boost != null)
                    {
                        weight *= (1.0 + boost.ShopItem!.BoostAmount);
                    }
                    weightedFish.Add((fish, weight));
                }

                var totalFishWeight = weightedFish.Sum(w => w.weight);
                var randomFishValue = RandomNumberGenerator.GetInt32(0, (int)(totalFishWeight * 1000)) / 1000.0;
                var currentFishWeight = 0.0;

                foreach (var (fish, weight) in weightedFish)
                {
                    currentFishWeight += weight;
                    if (randomFishValue < currentFishWeight)
                    {
                        return fish;
                    }
                }
            }

            return fishOfRarity.RandomElement();
        }

        private int CalculateStars(FishType fishType, List<UserFishingBoost> boosts)
        {
            var starBoosts = boosts.Where(b => b.ShopItem?.BoostType == FishingBoostType.StarBoost).ToList();
            var boostAmount = starBoosts.Sum(b => b.ShopItem?.BoostAmount ?? 0);

            // Apply secondary boost types
            var starBoosts2 = boosts.Where(b => b.ShopItem?.BoostType2 == FishingBoostType.StarBoost).ToList();
            boostAmount += starBoosts2.Sum(b => b.ShopItem?.BoostAmount2 ?? 0);

            // Apply tertiary boost types
            var starBoosts3 = boosts.Where(b => b.ShopItem?.BoostType3 == FishingBoostType.StarBoost).ToList();
            boostAmount += starBoosts3.Sum(b => b.ShopItem?.BoostAmount3 ?? 0);

            var threeStarChance = 5.0 + (boostAmount * 100);
            var twoStarChance = 20.0 + (boostAmount * 100);

            var roll = RandomNumberGenerator.GetInt32(0, 10000) / 100.0;
            if (roll < threeStarChance) return 3;
            if (roll < twoStarChance + threeStarChance) return 2;
            return 1;
        }

        private double CalculateWeight(FishType fishType, int stars, List<UserFishingBoost> boosts)
        {
            var weightBoosts = boosts.Where(b => b.ShopItem?.BoostType == FishingBoostType.WeightBoost).ToList();
            var boostMultiplier = 1.0 + weightBoosts.Sum(b => b.ShopItem?.BoostAmount ?? 0);

            // Apply secondary boost types
            var weightBoosts2 = boosts.Where(b => b.ShopItem?.BoostType2 == FishingBoostType.WeightBoost).ToList();
            boostMultiplier += weightBoosts2.Sum(b => b.ShopItem?.BoostAmount2 ?? 0);

            // Apply tertiary boost types
            var weightBoosts3 = boosts.Where(b => b.ShopItem?.BoostType3 == FishingBoostType.WeightBoost).ToList();
            boostMultiplier += weightBoosts3.Sum(b => b.ShopItem?.BoostAmount3 ?? 0);

            // Star multipliers increase weight for higher quality catches
            var starMultiplier = stars switch
            {
                3 => 1.5,
                2 => 1.2,
                _ => 1.0
            };

            // Generate random weight between 0.8x and 1.13x of base weight
            var minMultiplier = 0.8;
            var maxMultiplier = 1.13;
            var randomValue = RandomNumberGenerator.GetInt32(0, 10000) / 10000.0;
            var weightMultiplier = minMultiplier + (randomValue * (maxMultiplier - minMultiplier));

            var weight = fishType.BaseWeight * weightMultiplier * starMultiplier * boostMultiplier;
            return Math.Round(weight, 2);
        }

        private int CalculateGold(FishType fishType, int stars, double actualWeight)
        {
            // Star levels determine consecutive, non-overlapping gold ranges
            var (minMultiplier, maxMultiplier) = stars switch
            {
                3 => (1.25, 1.41),
                2 => (1.0, 1.25),
                _ => (0.75, 1.0)
            };

            var randomValue = RandomNumberGenerator.GetInt32((int)(minMultiplier * 1000), (int)(maxMultiplier * 1000)) / 1000.0;

            // Weight influences gold: heavier fish relative to base weight = more gold
            // Weight multiplier ranges from 0.8x to 1.13x (based on CalculateWeight), which translates to 0.9x to 1.065x gold
            // This ensures heavier catches are always worth more, but not dramatically so
            var weightMultiplier = 0.9 + ((actualWeight / fishType.BaseWeight - 0.8) / (1.13 - 0.8) * 0.165);
            weightMultiplier = Math.Max(0.9, Math.Min(1.065, weightMultiplier)); // Clamp between 0.9x and 1.065x

            return Math.Max(1, (int)(fishType.BaseGold * randomValue * weightMultiplier));
        }

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

        public async Task<int> GenerateDefaultShopItems()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var existingItems = await context.FishingShopItems.ToListAsync();
            var itemsToAdd = new List<FishingShopItem>();

            var existingNames = new HashSet<string>(
                existingItems.Select(i => i.Name), 
                StringComparer.OrdinalIgnoreCase
            );

            bool ItemExists(string name) => 
                existingNames.Contains(name) || 
                itemsToAdd.Any(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            // ===== RODS (General Rarity Boost) =====
            if (!ItemExists("Bamboo Rod"))
                itemsToAdd.Add(new FishingShopItem { Name = "Bamboo Rod", Description = "A basic bamboo fishing rod (+5% rare fish)", Cost = 150, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.05, EquipmentSlot = EquipmentSlot.Rod, IsConsumable = false, Enabled = true });

            if (!ItemExists("Fiberglass Rod"))
                itemsToAdd.Add(new FishingShopItem { Name = "Fiberglass Rod", Description = "Flexible and durable (+10% rare fish)", Cost = 400, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.10, EquipmentSlot = EquipmentSlot.Rod, IsConsumable = false, Enabled = true });

            if (!ItemExists("Carbon Fiber Rod"))
                itemsToAdd.Add(new FishingShopItem { Name = "Carbon Fiber Rod", Description = "Lightweight and strong (+15% rare fish)", Cost = 1000, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.15, EquipmentSlot = EquipmentSlot.Rod, IsConsumable = false, Enabled = true });

            if (!ItemExists("Legendary Rod"))
                itemsToAdd.Add(new FishingShopItem { Name = "Legendary Rod", Description = "The stuff of legends (+25% rare fish)", Cost = 2500, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.25, EquipmentSlot = EquipmentSlot.Rod, IsConsumable = false, Enabled = true });

            // ===== REELS (Star Boost - Precision/Control) =====
            if (!ItemExists("Basic Reel"))
                itemsToAdd.Add(new FishingShopItem { Name = "Basic Reel", Description = "Simple spinning reel (+5% star quality)", Cost = 200, BoostType = FishingBoostType.StarBoost, BoostAmount = 0.05, EquipmentSlot = EquipmentSlot.Reel, IsConsumable = false, Enabled = true });

            if (!ItemExists("Precision Reel"))
                itemsToAdd.Add(new FishingShopItem { Name = "Precision Reel", Description = "Smooth drag system (+10% star quality)", Cost = 500, BoostType = FishingBoostType.StarBoost, BoostAmount = 0.10, EquipmentSlot = EquipmentSlot.Reel, IsConsumable = false, Enabled = true });

            if (!ItemExists("Professional Reel"))
                itemsToAdd.Add(new FishingShopItem { Name = "Professional Reel", Description = "Tournament-grade precision (+15% star quality)", Cost = 1200, BoostType = FishingBoostType.StarBoost, BoostAmount = 0.15, EquipmentSlot = EquipmentSlot.Reel, IsConsumable = false, Enabled = true });

            if (!ItemExists("Master Reel"))
                itemsToAdd.Add(new FishingShopItem { Name = "Master Reel", Description = "Ultimate control and smoothness (+20% star quality)", Cost = 3000, BoostType = FishingBoostType.StarBoost, BoostAmount = 0.20, EquipmentSlot = EquipmentSlot.Reel, IsConsumable = false, Enabled = true });

            // ===== LINES (Weight Boost - Strength) =====
            if (!ItemExists("Monofilament Line"))
                itemsToAdd.Add(new FishingShopItem { Name = "Monofilament Line", Description = "Basic fishing line (+10% weight)", Cost = 175, BoostType = FishingBoostType.WeightBoost, BoostAmount = 0.10, EquipmentSlot = EquipmentSlot.Line, IsConsumable = false, Enabled = true });

            if (!ItemExists("Braided Line"))
                itemsToAdd.Add(new FishingShopItem { Name = "Braided Line", Description = "High strength, low stretch (+20% weight)", Cost = 450, BoostType = FishingBoostType.WeightBoost, BoostAmount = 0.20, EquipmentSlot = EquipmentSlot.Line, IsConsumable = false, Enabled = true });

            if (!ItemExists("Fluorocarbon Line"))
                itemsToAdd.Add(new FishingShopItem { Name = "Fluorocarbon Line", Description = "Nearly invisible, very strong (+30% weight)", Cost = 1100, BoostType = FishingBoostType.WeightBoost, BoostAmount = 0.30, EquipmentSlot = EquipmentSlot.Line, IsConsumable = false, Enabled = true });

            if (!ItemExists("Titanium Wire"))
                itemsToAdd.Add(new FishingShopItem { Name = "Titanium Wire", Description = "Unbreakable fishing wire (+45% weight)", Cost = 2800, BoostType = FishingBoostType.WeightBoost, BoostAmount = 0.45, EquipmentSlot = EquipmentSlot.Line, IsConsumable = false, Enabled = true });

            // ===== HOOKS (Star Boost - Quality) =====
            if (!ItemExists("Standard Hook"))
                itemsToAdd.Add(new FishingShopItem { Name = "Standard Hook", Description = "Reliable J-hook (+5% star quality)", Cost = 150, BoostType = FishingBoostType.StarBoost, BoostAmount = 0.05, EquipmentSlot = EquipmentSlot.Hook, IsConsumable = false, Enabled = true });

            if (!ItemExists("Circle Hook"))
                itemsToAdd.Add(new FishingShopItem { Name = "Circle Hook", Description = "Self-setting design (+10% star quality)", Cost = 400, BoostType = FishingBoostType.StarBoost, BoostAmount = 0.10, EquipmentSlot = EquipmentSlot.Hook, IsConsumable = false, Enabled = true });

            if (!ItemExists("Treble Hook"))
                itemsToAdd.Add(new FishingShopItem { Name = "Treble Hook", Description = "Triple the catching power (+15% star quality)", Cost = 1000, BoostType = FishingBoostType.StarBoost, BoostAmount = 0.15, EquipmentSlot = EquipmentSlot.Hook, IsConsumable = false, Enabled = true });

            if (!ItemExists("Diamond Hook"))
                itemsToAdd.Add(new FishingShopItem { Name = "Diamond Hook", Description = "Razor-sharp perfection (+22% star quality)", Cost = 2500, BoostType = FishingBoostType.StarBoost, BoostAmount = 0.22, EquipmentSlot = EquipmentSlot.Hook, IsConsumable = false, Enabled = true });

            // ===== TACKLE BOXES (Multiple Boosts) =====
            if (!ItemExists("Basic Tackle Box"))
                itemsToAdd.Add(new FishingShopItem { Name = "Basic Tackle Box", Description = "Organized storage (+5% rarity, +5% weight)", Cost = 300, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.05, BoostType2 = FishingBoostType.WeightBoost, BoostAmount2 = 0.05, EquipmentSlot = EquipmentSlot.TackleBox, IsConsumable = false, Enabled = true });

            if (!ItemExists("Pro Tackle Box"))
                itemsToAdd.Add(new FishingShopItem { Name = "Pro Tackle Box", Description = "Everything you need (+10% rarity, +10% weight)", Cost = 800, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.10, BoostType2 = FishingBoostType.WeightBoost, BoostAmount2 = 0.10, EquipmentSlot = EquipmentSlot.TackleBox, IsConsumable = false, Enabled = true });

            if (!ItemExists("Master Tackle Box"))
                itemsToAdd.Add(new FishingShopItem { Name = "Master Tackle Box", Description = "Complete arsenal (+15% rarity, +15% weight)", Cost = 2000, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.15, BoostType2 = FishingBoostType.WeightBoost, BoostAmount2 = 0.15, EquipmentSlot = EquipmentSlot.TackleBox, IsConsumable = false, Enabled = true });

            // ===== NETS (Weight Bonus) =====
            if (!ItemExists("Landing Net"))
                itemsToAdd.Add(new FishingShopItem { Name = "Landing Net", Description = "Helps land bigger fish (+15% weight)", Cost = 350, BoostType = FishingBoostType.WeightBoost, BoostAmount = 0.15, EquipmentSlot = EquipmentSlot.Net, IsConsumable = false, Enabled = true });

            if (!ItemExists("Knotless Net"))
                itemsToAdd.Add(new FishingShopItem { Name = "Knotless Net", Description = "Gentle on fish, strong hold (+25% weight)", Cost = 900, BoostType = FishingBoostType.WeightBoost, BoostAmount = 0.25, EquipmentSlot = EquipmentSlot.Net, IsConsumable = false, Enabled = true });

            if (!ItemExists("Tournament Net"))
                itemsToAdd.Add(new FishingShopItem { Name = "Tournament Net", Description = "Professional-grade netting (+35% weight)", Cost = 2200, BoostType = FishingBoostType.WeightBoost, BoostAmount = 0.35, EquipmentSlot = EquipmentSlot.Net, IsConsumable = false, Enabled = true });

            // ===== CONSUMABLE BAITS (Bait Slot) =====
            if (!ItemExists("Worms"))
                itemsToAdd.Add(new FishingShopItem { Name = "Worms", Description = "Classic bait for all fish (+15% rarity, 5 uses)", Cost = 75, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.15, EquipmentSlot = EquipmentSlot.Bait, IsConsumable = true, MaxUses = 5, Enabled = true });

            if (!ItemExists("Minnows"))
                itemsToAdd.Add(new FishingShopItem { Name = "Minnows", Description = "Live bait for predators (+20% rarity, 5 uses)", Cost = 120, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.20, EquipmentSlot = EquipmentSlot.Bait, IsConsumable = true, MaxUses = 5, Enabled = true });

            if (!ItemExists("Premium Bait"))
                itemsToAdd.Add(new FishingShopItem { Name = "Premium Bait", Description = "Irresistible to rare fish (+30% rarity, 5 uses)", Cost = 200, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.30, EquipmentSlot = EquipmentSlot.Bait, IsConsumable = true, MaxUses = 5, Enabled = true });

            if (!ItemExists("Golden Bait"))
                itemsToAdd.Add(new FishingShopItem { Name = "Golden Bait", Description = "Legendary attractant (+50% rarity, 3 uses)", Cost = 350, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.50, EquipmentSlot = EquipmentSlot.Bait, IsConsumable = true, MaxUses = 3, Enabled = true });

            // ===== CONSUMABLE LURES (Lure Slot) =====
            if (!ItemExists("Spoon Lure"))
                itemsToAdd.Add(new FishingShopItem { Name = "Spoon Lure", Description = "Flashy wobbling action (+12% rarity, 8 uses)", Cost = 90, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.12, EquipmentSlot = EquipmentSlot.Lure, IsConsumable = true, MaxUses = 8, Enabled = true });

            if (!ItemExists("Crankbait"))
                itemsToAdd.Add(new FishingShopItem { Name = "Crankbait", Description = "Deep diving lure (+18% rarity, 7 uses)", Cost = 140, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.18, EquipmentSlot = EquipmentSlot.Lure, IsConsumable = true, MaxUses = 7, Enabled = true });

            if (!ItemExists("Jerkbait"))
                itemsToAdd.Add(new FishingShopItem { Name = "Jerkbait", Description = "Erratic swimming motion (+25% rarity, 6 uses)", Cost = 220, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.25, EquipmentSlot = EquipmentSlot.Lure, IsConsumable = true, MaxUses = 6, Enabled = true });

            if (!ItemExists("TopWater Popper"))
                itemsToAdd.Add(new FishingShopItem { Name = "TopWater Popper", Description = "Surface explosion action (+35% rarity, 5 uses)", Cost = 320, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.35, EquipmentSlot = EquipmentSlot.Lure, IsConsumable = true, MaxUses = 5, Enabled = true });

            if (!ItemExists("Swimbait"))
                itemsToAdd.Add(new FishingShopItem { Name = "Swimbait", Description = "Realistic swimming lure (+45% rarity, 5 uses)", Cost = 400, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.45, EquipmentSlot = EquipmentSlot.Lure, IsConsumable = true, MaxUses = 5, Enabled = true });

            // ===== FISH-SPECIFIC ITEMS (Generated Dynamically) =====
            var allFish = await context.FishTypes.Where(f => f.Enabled).ToListAsync();
            var targetFish = allFish.Where(f => f.Rarity >= FishRarity.Rare).ToList();

            foreach (var fish in targetFish)
            {
                // Fish-Specific Bait
                var baitName = $"{fish.Name} Bait";
                if (!ItemExists(baitName))
                {
                    var baitCost = fish.Rarity switch
                    {
                        FishRarity.Legendary => 800,
                        FishRarity.Epic => 450,
                        FishRarity.Rare => 250,
                        _ => 150
                    };

                    var baitBoost = fish.Rarity switch
                    {
                        FishRarity.Legendary => 3.0,   // 300% boost
                        FishRarity.Epic => 2.0,        // 200% boost
                        FishRarity.Rare => 1.5,        // 150% boost
                        _ => 1.0
                    };

                    var baitUses = fish.Rarity switch
                    {
                        FishRarity.Legendary => 3,
                        FishRarity.Epic => 5,
                        _ => 7
                    };

                    itemsToAdd.Add(new FishingShopItem
                    {
                        Name = baitName,
                        Description = $"Specialized bait for {fish.Name} ({baitUses} uses)",
                        Cost = baitCost,
                        BoostType = FishingBoostType.SpecificFishBoost,
                        BoostAmount = baitBoost,
                        TargetFishTypeId = fish.Id,
                        IsConsumable = true,
                        MaxUses = baitUses,
                        Enabled = true,
                        EquipmentSlot = EquipmentSlot.Bait
                    });
                }

                // Fish-Specific Lure
                var lureName = $"{fish.Name} Lure";
                if (!ItemExists(lureName))
                {
                    var lureCost = fish.Rarity switch
                    {
                        FishRarity.Legendary => 1000,
                        FishRarity.Epic => 600,
                        FishRarity.Rare => 350,
                        _ => 200
                    };

                    var lureBoost = fish.Rarity switch
                    {
                        FishRarity.Legendary => 4.0,   // 400% boost  
                        FishRarity.Epic => 2.5,        // 250% boost
                        FishRarity.Rare => 1.8,        // 180% boost
                        _ => 1.2
                    };

                    var lureUses = fish.Rarity switch
                    {
                        FishRarity.Legendary => 5,
                        FishRarity.Epic => 7,
                        _ => 10
                    };

                    itemsToAdd.Add(new FishingShopItem
                    {
                        Name = lureName,
                        Description = $"Premium lure designed for {fish.Name} ({lureUses} uses)",
                        Cost = lureCost,
                        BoostType = FishingBoostType.SpecificFishBoost,
                        BoostAmount = lureBoost,
                        TargetFishTypeId = fish.Id,
                        IsConsumable = true,
                        MaxUses = lureUses,
                        Enabled = true,
                        EquipmentSlot = EquipmentSlot.Lure
                    });
                }
            }

            if (itemsToAdd.Any())
            {
                context.FishingShopItems.AddRange(itemsToAdd);
                await context.SaveChangesAsync();
            }

            return itemsToAdd.Count;
        }

        public async Task<FishingSimulationResult> SimulateFishing(int iterations, bool useBoostMode, double boostModeMultiplier, List<int> shopItemIds)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var result = new FishingSimulationResult
            {
                TotalIterations = iterations,
                BoostModeUsed = useBoostMode,
                BoostModeMultiplier = boostModeMultiplier
            };

            // Initialize counters
            foreach (FishRarity rarity in Enum.GetValues(typeof(FishRarity)))
            {
                result.RarityCounts[rarity] = 0;
            }
            result.StarCounts[1] = 0;
            result.StarCounts[2] = 0;
            result.StarCounts[3] = 0;

            // Get enabled fish types
            var fishTypes = await context.FishTypes.Where(f => f.Enabled).ToListAsync();
            if (!fishTypes.Any())
            {
                throw new InvalidOperationException("No fish types available for simulation");
            }

            // Get shop items for simulation
            var shopItems = await context.FishingShopItems
                .Where(i => shopItemIds.Contains(i.Id))
                .ToListAsync();

            // Create mock user boosts from shop items
            var mockBoosts = shopItems.Select(item => new UserFishingBoost
            {
                UserId = "simulation",
                ShopItemId = item.Id,
                ShopItem = item,
                IsEquipped = true,
                RemainingUses = 999
            }).ToList();

            result.ItemsUsed = shopItems.Select(i => i.Name).ToList();

            // Get or create settings
            var settings = await GetSettings();
            if (settings == null)
            {
                settings = new FishingSettings();
            }

            // Override boost mode settings for simulation
            var simulationSettings = new FishingSettings
            {
                BoostMode = useBoostMode,
                BoostModeRarityMultiplier = boostModeMultiplier,
                RarityUncommonThreshold = settings.RarityUncommonThreshold,
                RarityRareThreshold = settings.RarityRareThreshold,
                RarityEpicThreshold = settings.RarityEpicThreshold,
                RarityLegendaryThreshold = settings.RarityLegendaryThreshold
            };

            var totalWeight = 0.0;
            var totalGold = 0;
            var minWeight = double.MaxValue;
            var maxWeight = 0.0;
            var heaviestFishName = string.Empty;

            // Run simulations
            for (int i = 0; i < iterations; i++)
            {
                var fish = SelectRandomFish(fishTypes, simulationSettings, mockBoosts);
                var stars = CalculateStars(fish, mockBoosts);
                var weight = CalculateWeight(fish, stars, mockBoosts);
                var gold = CalculateGold(fish, stars, weight);

                // Update counters
                result.RarityCounts[fish.Rarity]++;

                if (!result.FishCounts.ContainsKey(fish.Name))
                    result.FishCounts[fish.Name] = 0;
                result.FishCounts[fish.Name]++;

                result.StarCounts[stars]++;

                totalWeight += weight;
                totalGold += gold;

                if (weight < minWeight)
                    minWeight = weight;

                if (weight > maxWeight)
                {
                    maxWeight = weight;
                    heaviestFishName = fish.Name;
                }
            }

            // Calculate statistics
            result.AverageWeight = Math.Round(totalWeight / iterations, 2);
            result.AverageGold = Math.Round((double)totalGold / iterations, 2);
            result.TotalGold = totalGold;
            result.MinWeight = Math.Round(minWeight, 2);
            result.MaxWeight = Math.Round(maxWeight, 2);
            result.HeaviestFish = heaviestFishName;
            result.MostCommonFish = result.FishCounts.OrderByDescending(kvp => kvp.Value).First().Key;

            return result;
        }

        public async Task<List<LeaderPosition>> GetTotalGoldLeaderboard(int count = 50)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Get top players by total gold
            var topPlayers = await context.FishingGolds
                .OrderByDescending(g => g.TotalGold)
                .Take(count)
                .ToListAsync();

            // Map to LeaderPosition with rank
            var leaderboard = topPlayers.Select((gold, index) => new LeaderPosition
            {
                Rank = index + 1,
                Name = gold.Username,
                Amount = gold.TotalGold
            }).ToList();

            return leaderboard;
        }

        public async Task<List<FishCatch>> GetMostValuableCatchesLeaderboard(int count = 50)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Get top catches by gold earned
            var topCatches = await context.FishCatches
                .Include(c => c.FishType)
                .OrderByDescending(c => c.GoldEarned)
                .ThenByDescending(c => c.CaughtAt)
                .Take(count)
                .ToListAsync();

            return topCatches;
        }

        public async Task<Dictionary<string, FishProbability>> CalculateCatchProbabilities(List<int> shopItemIds)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Get enabled fish types
            var fishTypes = await context.FishTypes.Where(f => f.Enabled).ToListAsync();
            if (!fishTypes.Any())
            {
                return new Dictionary<string, FishProbability>();
            }

            // Get settings
            var settings = await GetSettings();

            // Get shop items
            var shopItems = await context.FishingShopItems
                .Include(s => s.TargetFishType)
                .Where(i => shopItemIds.Contains(i.Id))
                .ToListAsync();

            // Create mock boosts
            var mockBoosts = shopItems.Select(item => new UserFishingBoost
            {
                UserId = "calculation",
                ShopItemId = item.Id,
                ShopItem = item,
                IsEquipped = true,
                RemainingUses = 999
            }).ToList();

            // Calculate rarity weights
            var rarityWeights = new Dictionary<FishRarity, double>
            {
                { FishRarity.Common, 50.0 },
                { FishRarity.Uncommon, 30.0 },
                { FishRarity.Rare, 15.0 },
                { FishRarity.Epic, 4.0 },
                { FishRarity.Legendary, 1.0 }
            };

            if (settings?.BoostMode == true)
            {
                var multiplier = settings.BoostModeRarityMultiplier;
                rarityWeights[FishRarity.Uncommon] *= multiplier;
                rarityWeights[FishRarity.Rare] *= multiplier;
                rarityWeights[FishRarity.Epic] *= multiplier;
                rarityWeights[FishRarity.Legendary] *= multiplier;
            }

            foreach (var boost in mockBoosts)
            {
                if (boost.ShopItem?.BoostType == FishingBoostType.GeneralRarityBoost)
                {
                    foreach (var rarity in rarityWeights.Keys.ToList())
                    {
                        if (rarity != FishRarity.Common)
                        {
                            rarityWeights[rarity] *= (1.0 + boost.ShopItem.BoostAmount);
                        }
                    }
                }
                else if (boost.ShopItem?.BoostType == FishingBoostType.SpecificFishBoost && 
                         boost.ShopItem.TargetFishTypeId != null)
                {
                    var targetFish = fishTypes.FirstOrDefault(f => f.Id == boost.ShopItem.TargetFishTypeId);
                    if (targetFish != null)
                    {
                        rarityWeights[targetFish.Rarity] *= (1.0 + boost.ShopItem.BoostAmount);
                    }
                }
            }

            var totalRarityWeight = rarityWeights.Values.Sum();

            // Calculate probabilities for each fish
            var probabilities = new Dictionary<string, FishProbability>();

            foreach (var fish in fishTypes)
            {
                var rarityChance = rarityWeights[fish.Rarity] / totalRarityWeight;

                // Get fish of same rarity
                var fishOfRarity = fishTypes.Where(f => f.Rarity == fish.Rarity).ToList();

                // Calculate specific fish weight within rarity
                var specificBoosts = mockBoosts.Where(b => 
                    b.ShopItem?.BoostType == FishingBoostType.SpecificFishBoost && 
                    b.ShopItem.TargetFishTypeId != null).ToList();

                double withinRarityChance;
                if (specificBoosts.Any())
                {
                    var weightedFish = new List<(FishType fish, double weight)>();
                    foreach (var f in fishOfRarity)
                    {
                        var weight = 1.0;
                        var boost = specificBoosts.FirstOrDefault(b => b.ShopItem?.TargetFishTypeId == f.Id);
                        if (boost != null)
                        {
                            weight *= (1.0 + boost.ShopItem!.BoostAmount);
                        }
                        weightedFish.Add((f, weight));
                    }

                    var totalFishWeight = weightedFish.Sum(w => w.weight);
                    var fishWeight = weightedFish.First(w => w.fish.Id == fish.Id).weight;
                    withinRarityChance = fishWeight / totalFishWeight;
                }
                else
                {
                    withinRarityChance = 1.0 / fishOfRarity.Count;
                }

                var overallChance = rarityChance * withinRarityChance;

                probabilities[fish.Name] = new FishProbability
                {
                    FishId = fish.Id,
                    FishName = fish.Name,
                    Rarity = fish.Rarity,
                    RarityChance = Math.Round(rarityChance * 100, 4),
                    WithinRarityChance = Math.Round(withinRarityChance * 100, 4),
                    OverallChance = Math.Round(overallChance * 100, 4),
                    ExpectedAttemptsForOneCatch = overallChance > 0 ? (int)Math.Ceiling(1.0 / overallChance) : 0
                };
            }

            return probabilities;
        }

        public async Task<RarityProbability> CalculateRarityProbabilities(bool useBoostMode, double boostModeMultiplier, List<int> shopItemIds)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var fishTypes = await context.FishTypes.Where(f => f.Enabled).ToListAsync();

            // Get shop items
            var shopItems = await context.FishingShopItems
                .Include(s => s.TargetFishType)
                .Where(i => shopItemIds.Contains(i.Id))
                .ToListAsync();

            // Create mock boosts
            var mockBoosts = shopItems.Select(item => new UserFishingBoost
            {
                UserId = "calculation",
                ShopItemId = item.Id,
                ShopItem = item,
                IsEquipped = true,
                RemainingUses = 999
            }).ToList();

            var rarityWeights = new Dictionary<FishRarity, double>
            {
                { FishRarity.Common, 50.0 },
                { FishRarity.Uncommon, 30.0 },
                { FishRarity.Rare, 15.0 },
                { FishRarity.Epic, 4.0 },
                { FishRarity.Legendary, 1.0 }
            };

            if (useBoostMode)
            {
                rarityWeights[FishRarity.Uncommon] *= boostModeMultiplier;
                rarityWeights[FishRarity.Rare] *= boostModeMultiplier;
                rarityWeights[FishRarity.Epic] *= boostModeMultiplier;
                rarityWeights[FishRarity.Legendary] *= boostModeMultiplier;
            }

            foreach (var boost in mockBoosts)
            {
                if (boost.ShopItem?.BoostType == FishingBoostType.GeneralRarityBoost)
                {
                    foreach (var rarity in rarityWeights.Keys.ToList())
                    {
                        if (rarity != FishRarity.Common)
                        {
                            rarityWeights[rarity] *= (1.0 + boost.ShopItem.BoostAmount);
                        }
                    }
                }
                else if (boost.ShopItem?.BoostType == FishingBoostType.SpecificFishBoost && 
                         boost.ShopItem.TargetFishTypeId != null)
                {
                    var targetFish = fishTypes.FirstOrDefault(f => f.Id == boost.ShopItem.TargetFishTypeId);
                    if (targetFish != null)
                    {
                        rarityWeights[targetFish.Rarity] *= (1.0 + boost.ShopItem.BoostAmount);
                    }
                }
            }

            var totalWeight = rarityWeights.Values.Sum();

            var result = new RarityProbability
            {
                BoostModeActive = useBoostMode,
                BoostModeMultiplier = boostModeMultiplier,
                ItemsEquipped = shopItems.Select(i => i.Name).ToList(),
                Probabilities = new Dictionary<FishRarity, double>()
            };

            foreach (var kvp in rarityWeights)
            {
                result.Probabilities[kvp.Key] = Math.Round((kvp.Value / totalWeight) * 100, 4);
            }

            return result;
        }
    }
}
