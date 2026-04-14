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
            var (minMultiplier, maxMultiplier) = stars switch
            {
                3 => (1.25, 1.41),
                2 => (1.0, 1.25),
                _ => (0.75, 1.0)
            };

            var randomValue = RandomNumberGenerator.GetInt32((int)(minMultiplier * 1000), (int)(maxMultiplier * 1000)) / 1000.0;
            return Math.Max(1, (int)(fishType.BaseGold * randomValue));
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

            // Helper to check if item already exists
            bool ItemExists(string name) => existingItems.Any(i => i.Name == name);

            // Equipment - Fishing Rods (Rod Slot, Permanent, Rarity Boost)
            if (!ItemExists("Basic Rod"))
                itemsToAdd.Add(new FishingShopItem { Name = "Basic Rod", Description = "A simple fishing rod for beginners", Cost = 200, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.05, EquipmentSlot = EquipmentSlot.Rod, IsConsumable = false, Enabled = true });

            if (!ItemExists("Quality Rod"))
                itemsToAdd.Add(new FishingShopItem { Name = "Quality Rod", Description = "A well-crafted rod for better catches", Cost = 500, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.10, EquipmentSlot = EquipmentSlot.Rod, IsConsumable = false, Enabled = true });

            if (!ItemExists("Expert Rod"))
                itemsToAdd.Add(new FishingShopItem { Name = "Expert Rod", Description = "A professional-grade fishing rod", Cost = 1200, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.15, EquipmentSlot = EquipmentSlot.Rod, IsConsumable = false, Enabled = true });

            if (!ItemExists("Master Rod"))
                itemsToAdd.Add(new FishingShopItem { Name = "Master Rod", Description = "The ultimate fishing rod for masters", Cost = 3000, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.20, EquipmentSlot = EquipmentSlot.Rod, IsConsumable = false, Enabled = true });

            // Equipment - Fishing Lines (Lure Slot, Permanent, Weight Boost)
            if (!ItemExists("Basic Line"))
                itemsToAdd.Add(new FishingShopItem { Name = "Basic Line", Description = "Reliable fishing line", Cost = 250, BoostType = FishingBoostType.WeightBoost, BoostAmount = 0.10, EquipmentSlot = EquipmentSlot.Lure, IsConsumable = false, Enabled = true });

            if (!ItemExists("Strong Line"))
                itemsToAdd.Add(new FishingShopItem { Name = "Strong Line", Description = "Reinforced line for heavier catches", Cost = 600, BoostType = FishingBoostType.WeightBoost, BoostAmount = 0.20, EquipmentSlot = EquipmentSlot.Lure, IsConsumable = false, Enabled = true });

            if (!ItemExists("Titanium Line"))
                itemsToAdd.Add(new FishingShopItem { Name = "Titanium Line", Description = "High-tech line for serious anglers", Cost = 1500, BoostType = FishingBoostType.WeightBoost, BoostAmount = 0.35, EquipmentSlot = EquipmentSlot.Lure, IsConsumable = false, Enabled = true });

            if (!ItemExists("Diamond Line"))
                itemsToAdd.Add(new FishingShopItem { Name = "Diamond Line", Description = "The strongest line money can buy", Cost = 4000, BoostType = FishingBoostType.WeightBoost, BoostAmount = 0.50, EquipmentSlot = EquipmentSlot.Lure, IsConsumable = false, Enabled = true });

            // Equipment - Fishing Hooks (Accessory Slot, Permanent, Star Boost)
            if (!ItemExists("Sharp Hook"))
                itemsToAdd.Add(new FishingShopItem { Name = "Sharp Hook", Description = "A well-sharpened hook", Cost = 300, BoostType = FishingBoostType.StarBoost, BoostAmount = 0.05, EquipmentSlot = EquipmentSlot.Accessory, IsConsumable = false, Enabled = true });

            if (!ItemExists("Barbed Hook"))
                itemsToAdd.Add(new FishingShopItem { Name = "Barbed Hook", Description = "Hook with barbs for better grip", Cost = 800, BoostType = FishingBoostType.StarBoost, BoostAmount = 0.10, EquipmentSlot = EquipmentSlot.Accessory, IsConsumable = false, Enabled = true });

            if (!ItemExists("Master Hook"))
                itemsToAdd.Add(new FishingShopItem { Name = "Master Hook", Description = "Expertly crafted hook", Cost = 2000, BoostType = FishingBoostType.StarBoost, BoostAmount = 0.15, EquipmentSlot = EquipmentSlot.Accessory, IsConsumable = false, Enabled = true });

            if (!ItemExists("Legendary Hook"))
                itemsToAdd.Add(new FishingShopItem { Name = "Legendary Hook", Description = "The hook of legends", Cost = 5000, BoostType = FishingBoostType.StarBoost, BoostAmount = 0.20, EquipmentSlot = EquipmentSlot.Accessory, IsConsumable = false, Enabled = true });

            // Equipment - Fishing Reels (Reel Slot, Permanent, Rarity Boost)
            if (!ItemExists("Basic Reel"))
                itemsToAdd.Add(new FishingShopItem { Name = "Basic Reel", Description = "Simple but effective reel", Cost = 200, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.05, EquipmentSlot = EquipmentSlot.Reel, IsConsumable = false, Enabled = true });

            if (!ItemExists("Speed Reel"))
                itemsToAdd.Add(new FishingShopItem { Name = "Speed Reel", Description = "Fast retrieval for quick catches", Cost = 500, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.10, EquipmentSlot = EquipmentSlot.Reel, IsConsumable = false, Enabled = true });

            if (!ItemExists("Professional Reel"))
                itemsToAdd.Add(new FishingShopItem { Name = "Professional Reel", Description = "High-quality reel for pros", Cost = 1200, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.15, EquipmentSlot = EquipmentSlot.Reel, IsConsumable = false, Enabled = true });

            // Consumables - Baits and Lures (No Slot, Limited Uses)
            if (!ItemExists("Lucky Bait"))
                itemsToAdd.Add(new FishingShopItem { Name = "Lucky Bait", Description = "Increases rare fish chances (3 uses)", Cost = 100, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.25, IsConsumable = true, MaxUses = 3, Enabled = true });

            if (!ItemExists("Golden Lure"))
                itemsToAdd.Add(new FishingShopItem { Name = "Golden Lure", Description = "Attracts higher quality fish (5 uses)", Cost = 150, BoostType = FishingBoostType.StarBoost, BoostAmount = 0.15, IsConsumable = true, MaxUses = 5, Enabled = true });

            if (!ItemExists("Power Bait"))
                itemsToAdd.Add(new FishingShopItem { Name = "Power Bait", Description = "Catches heavier fish (10 uses)", Cost = 200, BoostType = FishingBoostType.WeightBoost, BoostAmount = 0.30, IsConsumable = true, MaxUses = 10, Enabled = true });

            if (!ItemExists("Supreme Bait"))
                itemsToAdd.Add(new FishingShopItem { Name = "Supreme Bait", Description = "Significantly boosts rare catches (5 uses)", Cost = 300, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.50, IsConsumable = true, MaxUses = 5, Enabled = true });

            if (!ItemExists("Mega Lure"))
                itemsToAdd.Add(new FishingShopItem { Name = "Mega Lure", Description = "Massive weight boost (5 uses)", Cost = 250, BoostType = FishingBoostType.WeightBoost, BoostAmount = 0.50, IsConsumable = true, MaxUses = 5, Enabled = true });

            // Fish-Specific Baits (dynamically generated for valuable fish)
            var allFish = await context.FishTypes.Where(f => f.Enabled).ToListAsync();
            var valuableFish = allFish.Where(f => 
                f.Rarity >= FishRarity.Rare || // All rare+ fish
                f.BaseGold >= 100 // Or fish worth 100+ gold
            ).ToList();

            foreach (var fish in valuableFish)
            {
                var itemName = $"{fish.Name} Bait";
                if (!ItemExists(itemName))
                {
                    // Price based on rarity and value
                    var baseCost = fish.Rarity switch
                    {
                        FishRarity.Legendary => 500,
                        FishRarity.Epic => 300,
                        FishRarity.Rare => 200,
                        FishRarity.Uncommon => 150,
                        _ => 100
                    };

                    // Adjust cost based on fish value
                    var costMultiplier = fish.BaseGold switch
                    {
                        >= 500 => 1.5,
                        >= 200 => 1.25,
                        >= 100 => 1.0,
                        _ => 0.75
                    };

                    var finalCost = (int)(baseCost * costMultiplier);

                    // Boost amount based on rarity (higher rarity = stronger boost)
                    var boostAmount = fish.Rarity switch
                    {
                        FishRarity.Legendary => 2.0,  // 200% increased chance
                        FishRarity.Epic => 1.5,       // 150% increased chance
                        FishRarity.Rare => 1.0,       // 100% increased chance
                        FishRarity.Uncommon => 0.75,  // 75% increased chance
                        _ => 0.5                      // 50% increased chance
                    };

                    var uses = fish.Rarity switch
                    {
                        FishRarity.Legendary => 3,
                        FishRarity.Epic => 5,
                        FishRarity.Rare => 5,
                        _ => 10
                    };

                    itemsToAdd.Add(new FishingShopItem
                    {
                        Name = itemName,
                        Description = $"Specialized bait that attracts {fish.Name} ({uses} uses)",
                        Cost = finalCost,
                        BoostType = FishingBoostType.SpecificFishBoost,
                        BoostAmount = boostAmount,
                        TargetFishTypeId = fish.Id,
                        IsConsumable = true,
                        MaxUses = uses,
                        Enabled = true
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
    }
}
