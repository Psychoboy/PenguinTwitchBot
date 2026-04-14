using DotNetTwitchBot.Bot.Core.Database;
using DotNetTwitchBot.Bot.Models.Fishing;
using DotNetTwitchBot.Extensions;
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

            userBoost.RemainingUses--;
            userBoost.LastUsedAt = DateTime.UtcNow;

            // If consumable and no uses left, remove the item
            if (userBoost.ShopItem!.IsConsumable && userBoost.RemainingUses <= 0)
            {
                userBoost.IsEquipped = false;
                // Optionally delete the item entirely
                // context.UserFishingBoosts.Remove(userBoost);
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
            var fishTypes = await GetAllFishTypes();
            if (fishTypes.Count == 0)
            {
                throw new InvalidOperationException("No fish types available");
            }

            var settings = await GetSettings();
            // Only get equipped items, not all boosts
            var userBoosts = await GetUserEquippedItems(userId);

            var fishType = SelectRandomFish(fishTypes, settings, userBoosts);
            var stars = CalculateStars(fishType, userBoosts);
            var weight = CalculateWeight(fishType, stars, userBoosts);
            var gold = CalculateGold(fishType, stars);

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

        private int CalculateGold(FishType fishType, int stars)
        {
            // Star levels determine consecutive, non-overlapping gold ranges
            // 1 Star: BaseGold × (1.0 to 1.25)
            // 2 Star: BaseGold × (1.25 to 1.375)
            // 3 Star: BaseGold × (1.375 to 1.41)
            var (minMultiplier, maxMultiplier) = stars switch
            {
                3 => (1.375, 1.41),
                2 => (1.25, 1.375),
                _ => (1.0, 1.25)
            };

            var randomValue = RandomNumberGenerator.GetInt32((int)(minMultiplier * 1000), (int)(maxMultiplier * 1000)) / 1000.0;
            return (int)(fishType.BaseGold * randomValue);
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
    }
}
