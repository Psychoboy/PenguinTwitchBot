using DotNetTwitchBot.Bot.Core.Database;
using DotNetTwitchBot.Bot.Models.Fishing;
using DotNetTwitchBot.Bot.Hubs;
using DotNetTwitchBot.Extensions;
using DotNetTwitchBot.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace DotNetTwitchBot.Bot.Commands.Fishing
{
    public class FishingService : IFishingService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<FishingService> _logger;
        private readonly IHubContext<MainHub> _hubContext;

        public FishingService(IServiceScopeFactory scopeFactory, ILogger<FishingService> logger, IHubContext<MainHub> hubContext)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _hubContext = hubContext;
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
            if (shopItem == null || !shopItem.Enabled || shopItem.IsAdminOnly)
            {
                throw new InvalidOperationException("Shop item not found, disabled, or not available for purchase");
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

        public async Task GiveItemToUser(string userId, int shopItemId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var shopItem = await context.FishingShopItems.FindAsync(shopItemId);
            if (shopItem == null)
            {
                throw new InvalidOperationException("Shop item not found");
            }

            var userBoost = new UserFishingBoost
            {
                UserId = userId,
                ShopItemId = shopItemId,
                RemainingUses = shopItem.MaxUses ?? -1 // -1 means unlimited
            };

            context.UserFishingBoosts.Add(userBoost);

            await context.SaveChangesAsync();
        }

        public async Task SellItem(string userId, int userBoostId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userBoost = await context.UserFishingBoosts
                .Include(b => b.ShopItem)
                .FirstOrDefaultAsync(b => b.Id == userBoostId && b.UserId == userId);
            if (userBoost == null || userBoost.ShopItem == null)
            {
                throw new InvalidOperationException("Item not found");
            }

            if(userBoost.IsEquipped)
            {
                throw new InvalidOperationException("Cannot sell an equipped item");
            }

            if(userBoost.RemainingUses >= 0)
            {
                throw new InvalidOperationException("Can not sell items with limited usage");
            }

            if(userBoost.ShopItem?.IsConsumable == true )
            {
                throw new InvalidOperationException("Cannot sell consumable items");
            }

            var sellPrice =  (int)(userBoost.ShopItem?.Cost * 0.15 ?? 0); // 15% of price back
            var gold = await context.FishingGolds.FirstOrDefaultAsync(g => g.UserId == userId);
            if (gold == null)
            {
               throw new InvalidOperationException("User gold record not found");
            }
            else
            {
                gold.TotalGold += sellPrice;
            }
            context.UserFishingBoosts.Remove(userBoost);
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
                else if (boost.ShopItem?.BoostType2 == FishingBoostType.SpecificFishBoost && 
                         boost.ShopItem.TargetFishTypeId != null)
                {
                    // Find the target fish and boost its rarity tier
                    var targetFish = fishTypes.FirstOrDefault(f => f.Id == boost.ShopItem.TargetFishTypeId);
                    if (targetFish != null)
                    {
                        // Boost the rarity tier of the target fish
                        rarityWeights[targetFish.Rarity] *= (1.0 + (boost.ShopItem.BoostAmount2 ?? 0));
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
                else if (boost.ShopItem?.BoostType3 == FishingBoostType.SpecificFishBoost && 
                         boost.ShopItem.TargetFishTypeId != null)
                {
                    // Find the target fish and boost its rarity tier
                    var targetFish = fishTypes.FirstOrDefault(f => f.Id == boost.ShopItem.TargetFishTypeId);
                    if (targetFish != null)
                    {
                        // Boost the rarity tier of the target fish
                        rarityWeights[targetFish.Rarity] *= (1.0 + (boost.ShopItem.BoostAmount3 ?? 0));
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

            // Get all specific fish boosts from all boost type slots
            var specificBoosts = boosts.Where(b => 
                (b.ShopItem?.BoostType == FishingBoostType.SpecificFishBoost ||
                 b.ShopItem?.BoostType2 == FishingBoostType.SpecificFishBoost ||
                 b.ShopItem?.BoostType3 == FishingBoostType.SpecificFishBoost) &&
                b.ShopItem.TargetFishTypeId != null).ToList();

            if (specificBoosts.Any())
            {
                var weightedFish = new List<(FishType fish, double weight)>();
                foreach (var fish in fishOfRarity)
                {
                    var weight = 1.0;
                    // Apply all specific boosts targeting this fish
                    foreach (var boost in specificBoosts.Where(b => b.ShopItem?.TargetFishTypeId == fish.Id))
                    {
                        if (boost.ShopItem?.BoostType == FishingBoostType.SpecificFishBoost)
                        {
                            weight *= (1.0 + boost.ShopItem.BoostAmount);
                        }
                        if (boost.ShopItem?.BoostType2 == FishingBoostType.SpecificFishBoost)
                        {
                            weight *= (1.0 + (boost.ShopItem.BoostAmount2 ?? 0));
                        }
                        if (boost.ShopItem?.BoostType3 == FishingBoostType.SpecificFishBoost)
                        {
                            weight *= (1.0 + (boost.ShopItem.BoostAmount3 ?? 0));
                        }
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
            var weightMultiplier = 1.0;
            if (fishType.BaseWeight > 0)
            {
                weightMultiplier = 0.9 + ((actualWeight / fishType.BaseWeight - 0.8) / (1.13 - 0.8) * 0.165);
                weightMultiplier = Math.Max(0.9, Math.Min(1.065, weightMultiplier)); // Clamp between 0.9x and 1.065x
            }

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

        public async Task<int> UpdateShopItemPrices(Dictionary<string, int> priceUpdates)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var updatedCount = 0;

            foreach (var (itemName, newPrice) in priceUpdates)
            {
                var item = await context.FishingShopItems
                    .FirstOrDefaultAsync(i => i.Name == itemName);

                if (item != null && item.Cost != newPrice)
                {
                    item.Cost = newPrice;
                    updatedCount++;
                }
            }

            if (updatedCount > 0)
            {
                await context.SaveChangesAsync();
            }

            return updatedCount;
        }

        public async Task<int> ApplyPriceMultiplier(double multiplier, bool permanentOnly = false, EquipmentSlot? slot = null)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var query = context.FishingShopItems.AsQueryable();

            // Filter by permanent items if specified
            if (permanentOnly)
            {
                query = query.Where(i => !i.IsConsumable);
            }

            // Filter by equipment slot if specified
            if (slot.HasValue)
            {
                query = query.Where(i => i.EquipmentSlot == slot.Value);
            }

            var items = await query.ToListAsync();
            var updatedCount = 0;

            foreach (var item in items)
            {
                var newPrice = (int)Math.Round(item.Cost * multiplier);
                if (newPrice != item.Cost && newPrice > 0)
                {
                    item.Cost = newPrice;
                    updatedCount++;
                }
            }

            if (updatedCount > 0)
            {
                await context.SaveChangesAsync();
            }

            return updatedCount;
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

            // Use the most recent username for display
            var topPlayers = await context.FishCatches
                .AsNoTracking()
                .GroupBy(c => c.UserId)
                .Select(g => new 
                { 
                    UserId = g.Key,
                    g.OrderByDescending(c => c.CaughtAt).First().Username,
                    TotalGoldEarned = g.Sum(c => c.GoldEarned) 
                })
                .OrderByDescending(g => g.TotalGoldEarned)
                .Take(count)
                .ToListAsync();

            var leaderboard = topPlayers.Select((player, index) => new LeaderPosition
            {
                Rank = index + 1,
                Name = player.Username,
                Amount = player.TotalGoldEarned
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

        public async Task<List<FishCatch>> GetRecentCatches(int count = 50)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Get most recent catches globally
            var recentCatches = await context.FishCatches
                .Include(c => c.FishType)
                .OrderByDescending(c => c.CaughtAt)
                .Take(count)
                .ToListAsync();

            return recentCatches;
        }

        public async Task<List<FishCatch>> GetUserRecentCatches(string userId, int count = 50)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Get most recent catches for specific user
            var userCatches = await context.FishCatches
                .Include(c => c.FishType)
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CaughtAt)
                .Take(count)
                .ToListAsync();

            return userCatches;
        }

        public async Task<Dictionary<int, FishProbability>> CalculateCatchProbabilities(List<int> shopItemIds)
        {
            var settings = await GetSettings();
            return await CalculateCatchProbabilities(settings?.BoostMode ?? false, settings?.BoostModeRarityMultiplier ?? 1.0, shopItemIds);
        }

        public async Task<Dictionary<int, FishProbability>> CalculateCatchProbabilities(bool useBoostMode, double boostModeMultiplier, List<int> shopItemIds)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Get enabled fish types
            var fishTypes = await context.FishTypes.Where(f => f.Enabled).ToListAsync();
            if (!fishTypes.Any())
            {
                return new Dictionary<int, FishProbability>();
            }

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
                else if (boost.ShopItem?.BoostType2 == FishingBoostType.SpecificFishBoost && 
                         boost.ShopItem.TargetFishTypeId != null)
                {
                    var targetFish = fishTypes.FirstOrDefault(f => f.Id == boost.ShopItem.TargetFishTypeId);
                    if (targetFish != null)
                    {
                        rarityWeights[targetFish.Rarity] *= (1.0 + (boost.ShopItem.BoostAmount2 ?? 0));
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
                else if (boost.ShopItem?.BoostType3 == FishingBoostType.SpecificFishBoost && 
                         boost.ShopItem.TargetFishTypeId != null)
                {
                    var targetFish = fishTypes.FirstOrDefault(f => f.Id == boost.ShopItem.TargetFishTypeId);
                    if (targetFish != null)
                    {
                        rarityWeights[targetFish.Rarity] *= (1.0 + (boost.ShopItem.BoostAmount3 ?? 0));
                    }
                }
            }

            var totalRarityWeight = rarityWeights.Values.Sum();

            // Calculate probabilities for each fish
            var probabilities = new Dictionary<int, FishProbability>();

            foreach (var fish in fishTypes)
            {
                var rarityChance = rarityWeights[fish.Rarity] / totalRarityWeight;

                // Get fish of same rarity
                var fishOfRarity = fishTypes.Where(f => f.Rarity == fish.Rarity).ToList();

                // Calculate specific fish weight within rarity
                var specificBoosts = mockBoosts.Where(b => 
                    (b.ShopItem?.BoostType == FishingBoostType.SpecificFishBoost ||
                     b.ShopItem?.BoostType2 == FishingBoostType.SpecificFishBoost ||
                     b.ShopItem?.BoostType3 == FishingBoostType.SpecificFishBoost) &&
                    b.ShopItem.TargetFishTypeId != null).ToList();

                double withinRarityChance;
                if (specificBoosts.Any())
                {
                    var weightedFish = new List<(FishType fish, double weight)>();
                    foreach (var f in fishOfRarity)
                    {
                        var weight = 1.0;
                        // Apply all specific boosts targeting this fish
                        foreach (var boost in specificBoosts.Where(b => b.ShopItem?.TargetFishTypeId == f.Id))
                        {
                            if (boost.ShopItem?.BoostType == FishingBoostType.SpecificFishBoost)
                            {
                                weight *= (1.0 + boost.ShopItem.BoostAmount);
                            }
                            if (boost.ShopItem?.BoostType2 == FishingBoostType.SpecificFishBoost)
                            {
                                weight *= (1.0 + (boost.ShopItem.BoostAmount2 ?? 0));
                            }
                            if (boost.ShopItem?.BoostType3 == FishingBoostType.SpecificFishBoost)
                            {
                                weight *= (1.0 + (boost.ShopItem.BoostAmount3 ?? 0));
                            }
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

                probabilities[fish.Id] = new FishProbability
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
                else if (boost.ShopItem?.BoostType2 == FishingBoostType.SpecificFishBoost && 
                         boost.ShopItem.TargetFishTypeId != null)
                {
                    var targetFish = fishTypes.FirstOrDefault(f => f.Id == boost.ShopItem.TargetFishTypeId);
                    if (targetFish != null)
                    {
                        rarityWeights[targetFish.Rarity] *= (1.0 + (boost.ShopItem.BoostAmount2 ?? 0));
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
                else if (boost.ShopItem?.BoostType3 == FishingBoostType.SpecificFishBoost && 
                         boost.ShopItem.TargetFishTypeId != null)
                {
                    var targetFish = fishTypes.FirstOrDefault(f => f.Id == boost.ShopItem.TargetFishTypeId);
                    if (targetFish != null)
                    {
                        rarityWeights[targetFish.Rarity] *= (1.0 + (boost.ShopItem.BoostAmount3 ?? 0));
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

        public async Task<FishingBalanceReport> AnalyzeGameBalance(DateTime? startDate = null, DateTime? endDate = null)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var report = new FishingBalanceReport
            {
                StartDate = startDate,
                EndDate = endDate
            };

            // Get catches within date range
            var catchesQuery = context.FishCatches.Include(c => c.FishType).AsQueryable();
            if (startDate.HasValue)
                catchesQuery = catchesQuery.Where(c => c.CaughtAt >= startDate.Value);
            if (endDate.HasValue)
                catchesQuery = catchesQuery.Where(c => c.CaughtAt <= endDate.Value);

            var catches = await catchesQuery.ToListAsync();
            report.TotalCatches = catches.Count;

            if (catches.Count == 0)
            {
                report.Summary = "No catches found in the specified date range.";
                return report;
            }

            // Get unique users
            var uniqueUserIds = catches.Select(c => c.UserId).Distinct().ToList();
            report.UniqueUsers = uniqueUserIds.Count;

            // Rarity Distribution
            var rarityGroups = catches.GroupBy(c => c.FishType?.Rarity ?? FishRarity.Common);
            foreach (var group in rarityGroups)
            {
                var count = group.Count();
                report.RarityDistribution[group.Key] = count;
                report.RarityPercentages[group.Key] = Math.Round((count / (double)report.TotalCatches) * 100, 2);
            }

            // Ensure all rarities are present
            foreach (FishRarity rarity in Enum.GetValues(typeof(FishRarity)))
            {
                if (!report.RarityDistribution.ContainsKey(rarity))
                {
                    report.RarityDistribution[rarity] = 0;
                    report.RarityPercentages[rarity] = 0;
                }
            }

            // Star Distribution
            var starGroups = catches.GroupBy(c => c.Stars);
            foreach (var group in starGroups)
            {
                var count = group.Count();
                report.StarDistribution[group.Key] = count;
                report.StarPercentages[group.Key] = Math.Round((count / (double)report.TotalCatches) * 100, 2);
            }

            // Gold Statistics
            var goldEarned = catches.Select(c => c.GoldEarned).ToList();
            report.TotalGoldEarned = goldEarned.Sum();
            report.AverageGoldPerCatch = Math.Round(goldEarned.Average(), 2);
            report.MinGoldEarned = goldEarned.Min();
            report.MaxGoldEarned = goldEarned.Max();

            var sortedGold = goldEarned.OrderBy(g => g).ToList();
            report.MedianGoldPerCatch = sortedGold.Count % 2 == 0
                ? (sortedGold[sortedGold.Count / 2 - 1] + sortedGold[sortedGold.Count / 2]) / 2.0
                : sortedGold[sortedGold.Count / 2];

            // Per-User Statistics
            var userCatchCounts = catches.GroupBy(c => c.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToList();
            report.AverageCatchesPerUser = Math.Round(userCatchCounts.Average(u => u.Count), 2);

            var userGoldTotals = catches.GroupBy(c => c.UserId)
                .Select(g => new { UserId = g.Key, Username = g.First().Username, Total = g.Sum(c => c.GoldEarned) })
                .OrderByDescending(u => u.Total)
                .ToList();
            report.AverageGoldPerUser = Math.Round(userGoldTotals.Average(u => u.Total), 2);

            // Top 10 users by catches
            report.TopUsersByCatches = catches
                .GroupBy(c => c.Username)
                .Select(g => new { Username = g.Key, Count = g.Count() })
                .OrderByDescending(u => u.Count)
                .Take(10)
                .ToDictionary(u => u.Username, u => u.Count);

            // Top 10 users by gold
            report.TopUsersByGold = userGoldTotals
                .Take(10)
                .ToDictionary(u => u.Username, u => u.Total);

            // Fish Distribution
            var fishGroups = catches.GroupBy(c => c.FishType?.Name ?? "Unknown");
            foreach (var group in fishGroups)
            {
                var count = group.Count();
                report.FishCatchCounts[group.Key] = count;
                report.FishCatchPercentages[group.Key] = Math.Round((count / (double)report.TotalCatches) * 100, 2);
            }

            report.MostCaughtFish = report.FishCatchCounts.OrderByDescending(f => f.Value).First().Key;
            report.LeastCaughtFish = report.FishCatchCounts.OrderBy(f => f.Value).First().Key;

            // Get settings for expected probabilities
            var settings = await GetSettings();
            report.BoostModeActive = settings?.BoostMode;
            report.BoostModeMultiplier = settings?.BoostModeRarityMultiplier;

            // Calculate expected probabilities (baseline, no items)
            var baselineProbs = await CalculateRarityProbabilities(
                settings?.BoostMode ?? false,
                settings?.BoostModeRarityMultiplier ?? 1.0,
                new List<int>()
            );

            foreach (var kvp in baselineProbs.Probabilities)
            {
                report.ExpectedRarityPercentages[kvp.Key] = kvp.Value;
                var actualPercent = report.RarityPercentages.GetValueOrDefault(kvp.Key, 0);
                report.RarityVariance[kvp.Key] = Math.Round(actualPercent - kvp.Value, 2);
            }

            // Calculate average catches per day for time-based affordability
            var daysInPeriod = 1.0;
            if (startDate.HasValue && endDate.HasValue)
            {
                daysInPeriod = Math.Max(1, (endDate.Value - startDate.Value).TotalDays);
            }
            else if (catches.Any())
            {
                var firstCatch = catches.Min(c => c.CaughtAt);
                var lastCatch = catches.Max(c => c.CaughtAt);
                daysInPeriod = Math.Max(1, (lastCatch - firstCatch).TotalDays);
            }

            var averageCatchesPerDay = report.TotalCatches / daysInPeriod;
            var averageGoldPerDay = report.TotalGoldEarned / daysInPeriod;

            // Item Affordability Analysis
            var shopItems = await context.FishingShopItems
                .Where(i => i.Enabled && !i.IsAdminOnly)
                .OrderBy(i => i.Cost)
                .ToListAsync();

            var userCurrentGold = await context.FishingGolds.ToListAsync();
            var goldTotals = userCurrentGold.Select(g => g.TotalGold).OrderBy(g => g).ToList();

            foreach (var item in shopItems)
            {
                var usersCanAfford = userCurrentGold.Count(g => g.TotalGold >= item.Cost);
                var percentCanAfford = report.UniqueUsers > 0
                    ? Math.Round((usersCanAfford / (double)report.UniqueUsers) * 100, 2)
                    : 0;

                // Use MEDIAN gold per catch for more representative affordability calculations
                var catchesNeeded = report.MedianGoldPerCatch > 0
                    ? item.Cost / report.MedianGoldPerCatch
                    : 0;

                // Time-based affordability calculations
                // Assuming 3 catches per session, 3 sessions per week = 9 catches/week for median player
                // At median gold per catch: 9 catches × median gold = weekly income
                var weeksToAfford3xWeek = report.MedianGoldPerCatch > 0
                    ? item.Cost / (9 * report.MedianGoldPerCatch)
                    : 0;

                // Convert to days for backwards compatibility
                var daysToAfford3xWeek = weeksToAfford3xWeek * 7;

                // For daily fishing (7 days × 3 catches = 21 catches/week vs 9 catches/week)
                var daysToAfford1xDay = daysToAfford3xWeek / (21.0 / 9.0);

                var medianGold = goldTotals.Count > 0
                    ? (goldTotals.Count % 2 == 0
                        ? (goldTotals[goldTotals.Count / 2 - 1] + goldTotals[goldTotals.Count / 2]) / 2.0
                        : goldTotals[goldTotals.Count / 2])
                    : 0;

                // Calculate cost per use for consumables
                var costPerUse = item.IsConsumable && item.MaxUses.HasValue && item.MaxUses > 0
                    ? (double)item.Cost / item.MaxUses.Value
                    : item.Cost;

                // Affordability rating for permanent equipment
                string affordabilityRating = string.Empty;
                if (!item.IsConsumable)
                {
                    // For permanent items, rate based on progression tiers
                    // Aligned with progressive baseline model (5 tiers over 26 weeks)
                    // Low=1-3wk, Mid=3-8wk, High=8-16wk, Top=16+ weeks
                    var weeksToAfford = weeksToAfford3xWeek;

                    if (weeksToAfford < 1) // Less than 1 week
                        affordabilityRating = "Too Cheap";
                    else if (weeksToAfford < 3) // 1-3 weeks (Low tier - first purchases)
                        affordabilityRating = "Low Tier";
                    else if (weeksToAfford < 8) // 3-8 weeks (Mid tier - second upgrade)
                        affordabilityRating = "Mid Tier";
                    else if (weeksToAfford < 16) // 8-16 weeks (High tier - third upgrade)
                        affordabilityRating = "High Tier";
                    else // 16+ weeks (Top tier - endgame)
                        affordabilityRating = "Top Tier";
                }

                // Value rating for consumables
                string valueRating = string.Empty;
                if (item.IsConsumable)
                {
                    // For consumables, evaluate if they're good gold sinks
                    // Compare cost per use vs MEDIAN gold earned (more representative of typical player)
                    if (report.MedianGoldPerCatch > 0)
                    {
                        var usesPerMedianCatch = report.MedianGoldPerCatch / costPerUse;

                        if (usesPerMedianCatch > 3) // Very cheap, can use 3+ times per median catch
                            valueRating = "Excellent Value";
                        else if (usesPerMedianCatch > 1.5) // Moderate cost
                            valueRating = "Fair Trade";
                        else if (usesPerMedianCatch > 0.5) // Expensive but viable
                            valueRating = "Gold Sink";
                        else // Very expensive
                            valueRating = "Premium Sink";
                    }
                }

                report.ItemAffordabilityAnalysis.Add(new ItemAffordability
                {
                    ItemName = item.Name,
                    Cost = item.Cost,
                    IsConsumable = item.IsConsumable,
                    MaxUses = item.MaxUses,
                    EquipmentSlot = item.EquipmentSlot?.ToString() ?? "None",
                    CostPerUse = Math.Round(costPerUse, 2),
                    UsersWhoCanAfford = usersCanAfford,
                    PercentageWhoCanAfford = percentCanAfford,
                    CatchesNeededToBuy = Math.Round(catchesNeeded, 1),
                    DaysToAfford3xWeek = Math.Round(daysToAfford3xWeek, 1),
                    DaysToAfford1xDay = Math.Round(daysToAfford1xDay, 1),
                    MedianUserGold = medianGold,
                    AffordabilityRating = affordabilityRating,
                    ValueRating = valueRating
                });
            }

            // Most Common Equipment (from UserFishingBoosts)
            var equipmentUsage = await context.UserFishingBoosts
                .Include(b => b.ShopItem)
                .Where(b => uniqueUserIds.Contains(b.UserId) && b.ShopItem != null)
                .GroupBy(b => b.ShopItem!.Name)
                .Select(g => new { ItemName = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToListAsync();

            report.MostCommonEquipment = equipmentUsage.ToDictionary(e => e.ItemName, e => e.Count);

            // Generate Summary
            var summaryLines = new List<string>
            {
                $"Analyzed {report.TotalCatches:N0} catches from {report.UniqueUsers} unique users.",
                $"Date Range: {(startDate?.ToString("yyyy-MM-dd") ?? "Beginning")} to {(endDate?.ToString("yyyy-MM-dd") ?? "Now")}",
                $"",
                $"Gold Economy:",
                $"  - Total Gold Earned: {report.TotalGoldEarned:N0}",
                $"  - Average per Catch: {report.AverageGoldPerCatch:N2}",
                $"  - Median per Catch: {report.MedianGoldPerCatch:N2}",
                $"  - Average per User: {report.AverageGoldPerUser:N0}",
                $"",
                $"Rarity Distribution (Actual vs Expected):"
            };

            foreach (var rarity in report.RarityPercentages.OrderBy(r => (int)r.Key))
            {
                var expected = report.ExpectedRarityPercentages.GetValueOrDefault(rarity.Key, 0);
                var variance = report.RarityVariance.GetValueOrDefault(rarity.Key, 0);
                var varianceSign = variance >= 0 ? "+" : "";
                summaryLines.Add($"  - {rarity.Key}: {rarity.Value}% (expected {expected}%, {varianceSign}{variance}%)");
            }

            summaryLines.Add("");
            summaryLines.Add($"Star Distribution:");
            foreach (var star in report.StarPercentages.OrderBy(s => s.Key))
            {
                summaryLines.Add($"  - {star.Key}?: {star.Value}% ({report.StarDistribution[star.Key]:N0} catches)");
            }

            report.Summary = string.Join(Environment.NewLine, summaryLines);

            // Generate Balance Recommendations
            var recommendations = new List<string>();

            // Check rarity variance
            foreach (var variance in report.RarityVariance)
            {
                if (Math.Abs(variance.Value) > 5) // More than 5% off
                {
                    if (variance.Value > 0)
                        recommendations.Add($"{variance.Key} fish are appearing {variance.Value}% MORE than expected. Consider reducing boost effectiveness.");
                    else
                        recommendations.Add($"{variance.Key} fish are appearing {Math.Abs(variance.Value)}% LESS than expected. Consider increasing boost effectiveness.");
                }
            }

            // Check item affordability
            var tooExpensive = report.ItemAffordabilityAnalysis.Count(i => i.AffordabilityRating == "Too Expensive");
            var tooCheap = report.ItemAffordabilityAnalysis.Count(i => i.AffordabilityRating == "Too Cheap");

            if (tooExpensive > shopItems.Count * 0.3)
                recommendations.Add($"{tooExpensive} items are too expensive for most users. Consider reducing prices or increasing gold rewards.");

            if (tooCheap > shopItems.Count * 0.3)
                recommendations.Add($"{tooCheap} items are too cheap. Consider increasing prices or reducing gold rewards.");

            // Check gold accumulation
            if (report.AverageGoldPerUser > 5000)
                recommendations.Add($"Users are accumulating high gold amounts (avg {report.AverageGoldPerUser:N0}). Consider adding higher-tier items or gold sinks.");

            if (report.AverageGoldPerUser < 500 && report.AverageCatchesPerUser > 10)
                recommendations.Add($"Users aren't accumulating enough gold despite fishing. Consider increasing base gold rewards or reducing item costs.");

            // Check star distribution
            var threeStarPercent = report.StarPercentages.GetValueOrDefault(3, 0);
            if (threeStarPercent < 3)
                recommendations.Add($"3? catches are very rare ({threeStarPercent}%). Consider increasing star boost effectiveness.");
            if (threeStarPercent > 15)
                recommendations.Add($"3? catches are too common ({threeStarPercent}%). Consider reducing star boost effectiveness.");

            report.BalanceRecommendations = recommendations;

            return report;
        }

        public async Task<double> CalculateBaselineExpectedGold()
        {
            // Calculate expected gold per catch for a player with NO equipment
            // This uses probability distributions and expected values, not actual catch data

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var fishTypes = await context.FishTypes.Where(f => f.Enabled).ToListAsync();
            if (!fishTypes.Any())
            {
                return 0.0;
            }

            // Base rarity weights (NO equipment bonuses)
            var rarityWeights = new Dictionary<FishRarity, double>
            {
                { FishRarity.Common, 50.0 },
                { FishRarity.Uncommon, 30.0 },
                { FishRarity.Rare, 15.0 },
                { FishRarity.Epic, 4.0 },
                { FishRarity.Legendary, 1.0 }
            };

            var totalRarityWeight = rarityWeights.Values.Sum();

            // Base star probabilities (NO equipment bonuses)
            // From CalculateStars: base 5% for 3-star, 20% for 2-star, 75% for 1-star
            var starProbabilities = new Dictionary<int, double>
            {
                { 1, 0.75 },  // 75% chance
                { 2, 0.20 },  // 20% chance
                { 3, 0.05 }   // 5% chance
            };

            double expectedGold = 0.0;

            // For each rarity tier
            foreach (var (rarity, rarityWeight) in rarityWeights)
            {
                var fishOfRarity = fishTypes.Where(f => f.Rarity == rarity).ToList();
                if (!fishOfRarity.Any()) continue;

                var rarityProbability = rarityWeight / totalRarityWeight;
                var perFishProbability = rarityProbability / fishOfRarity.Count;

                // For each fish in this rarity
                foreach (var fish in fishOfRarity)
                {
                    // For each star level
                    foreach (var (stars, starProb) in starProbabilities)
                    {
                        // Calculate expected weight (NO equipment bonuses)
                        // Base weight range: 0.8x to 1.13x (from CalculateWeight)
                        var avgWeightMultiplier = (0.8 + 1.13) / 2.0;  // 0.965

                        // Star multiplier for weight
                        var starWeightMultiplier = stars switch
                        {
                            3 => 1.5,
                            2 => 1.2,
                            _ => 1.0
                        };

                        var expectedWeight = fish.BaseWeight * avgWeightMultiplier * starWeightMultiplier;

                        // Calculate expected gold (from CalculateGold logic)
                        var (minGoldMultiplier, maxGoldMultiplier) = stars switch
                        {
                            3 => (1.25, 1.41),
                            2 => (1.0, 1.25),
                            _ => (0.75, 1.0)
                        };

                        var avgGoldMultiplier = (minGoldMultiplier + maxGoldMultiplier) / 2.0;

                        // Weight influence on gold (0.9 to 1.065 based on weight ratio)
                        var weightGoldMultiplier = 1.0;
                        if (fish.BaseWeight > 0)
                        {
                            var weightRatio = expectedWeight / fish.BaseWeight;
                            weightGoldMultiplier = 0.9 + ((weightRatio - 0.8) / (1.13 - 0.8) * 0.165);
                            weightGoldMultiplier = Math.Max(0.9, Math.Min(1.065, weightGoldMultiplier));
                        }

                        // Calculate gold for this combination
                        var gold = fish.BaseGold * avgGoldMultiplier * weightGoldMultiplier;
                        gold = Math.Max(1, gold);

                        // Weight by probability
                        expectedGold += perFishProbability * starProb * gold;
                    }
                }
            }

            return Math.Round(expectedGold, 2);
        }

        public async Task<double> CalculateProgressiveBaselineGold(int targetWeeks = 26)
        {
            // Calculate expected gold assuming player gradually upgrades equipment over time
            // This models realistic progression: starting naked, buying entry tier, then upgrading
            //
            // IMPORTANT: Players can sell old equipment for 15% of original cost when upgrading.
            // This resale value reduces net upgrade costs by ~10-15% at each tier transition:
            //   Entry (675g) ? Mid (2675g) = 2574g net after selling entry gear (101g back)
            //   Mid (2700g) ? High (5300g) = 4895g net after selling mid gear (405g back)  
            //   High (5300g) ? Top (11300g) = 10505g net after selling high gear (795g back)
            //
            // The tier durations below account for this economic factor, making progression
            // faster than raw item costs would suggest.

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var fishTypes = await context.FishTypes.Where(f => f.Enabled).ToListAsync();
            if (!fishTypes.Any())
            {
                return 0.0;
            }

            // Define progression tiers with equipment loadouts and time allocation
            // Tier durations account for 15% resale value speeding up upgrades
            var tiers = new List<ProgressionTier>
            {
                // Tier 0: No equipment (Weeks 1-2)
                // Saving for first equipment purchases (~675g needed)
                new ProgressionTier
                {
                    Name = "Naked",
                    Weeks = 2,
                    RarityBoost = 0.0,
                    StarBoost = 0.0,
                    WeightBoost = 0.0
                },

                // Tier 1: Entry equipment (Weeks 3-7)
                // Bamboo Rod (+5% rarity), Basic Reel (+5% star), Monofilament Line (+10% weight), Standard Hook (+5% star)
                // Using entry gear while saving ~2574g net for mid tier (after 101g resale)
                new ProgressionTier
                {
                    Name = "Entry",
                    Weeks = 5,
                    RarityBoost = 0.05,      // +5% from Bamboo Rod
                    StarBoost = 0.10,        // +5% from Basic Reel + 5% from Standard Hook
                    WeightBoost = 0.10       // +10% from Monofilament Line
                },

                // Tier 2: Mid equipment (Weeks 8-13)
                // Fiberglass Rod (+10% rarity), Precision Reel (+10% star), Braided Line (+20% weight), Circle Hook (+10% star)
                // Using mid gear while saving ~4895g net for high tier (after 405g resale)
                new ProgressionTier
                {
                    Name = "Mid",
                    Weeks = 6,
                    RarityBoost = 0.10,
                    StarBoost = 0.20,        // +10% + 10%
                    WeightBoost = 0.20
                },

                // Tier 3: High equipment (Weeks 14-20)
                // Carbon Fiber Rod (+15% rarity), Professional Reel (+15% star), Fluorocarbon Line (+30% weight), Treble Hook (+15% star)
                // Using high gear while saving ~10505g net for top tier (after 795g resale)
                new ProgressionTier
                {
                    Name = "High",
                    Weeks = 7,
                    RarityBoost = 0.15,
                    StarBoost = 0.30,        // +15% + 15%
                    WeightBoost = 0.30
                },

                // Tier 4: Top equipment (Weeks 21-26)
                // Legendary Rod (+25% rarity), Master Reel (+20% star), Titanium Wire (+45% weight), Diamond Hook (+22% star)
                // End-game equipment, no more upgrades needed
                new ProgressionTier
                {
                    Name = "Top",
                    Weeks = 6,
                    RarityBoost = 0.25,
                    StarBoost = 0.42,        // +20% + 22%
                    WeightBoost = 0.45
                }
            };

            // Adjust tier durations if target is different from 26 weeks
            if (targetWeeks != 26)
            {
                var scaleFactor = targetWeeks / 26.0;
                foreach (var tier in tiers)
                {
                    tier.Weeks = Math.Max(1, (int)Math.Round(tier.Weeks * scaleFactor));
                }
            }

            var totalWeeks = tiers.Sum(t => t.Weeks);
            double weightedGold = 0.0;

            // Calculate expected gold at each tier and weight by time spent
            foreach (var tier in tiers)
            {
                var tierGold = CalculateExpectedGoldWithBoosts(
                    fishTypes,
                    tier.RarityBoost,
                    tier.StarBoost,
                    tier.WeightBoost
                );

                var tierWeight = tier.Weeks / (double)totalWeeks;
                weightedGold += tierGold * tierWeight;
            }

            return Math.Round(weightedGold, 2);
        }

        private double CalculateExpectedGoldWithBoosts(
            List<FishType> fishTypes,
            double rarityBoost,
            double starBoost,
            double weightBoost)
        {
            // Base rarity weights
            var rarityWeights = new Dictionary<FishRarity, double>
            {
                { FishRarity.Common, 50.0 },
                { FishRarity.Uncommon, 30.0 },
                { FishRarity.Rare, 15.0 },
                { FishRarity.Epic, 4.0 },
                { FishRarity.Legendary, 1.0 }
            };

            // Apply rarity boost (affects all non-Common rarities)
            if (rarityBoost > 0)
            {
                rarityWeights[FishRarity.Uncommon] *= (1.0 + rarityBoost);
                rarityWeights[FishRarity.Rare] *= (1.0 + rarityBoost);
                rarityWeights[FishRarity.Epic] *= (1.0 + rarityBoost);
                rarityWeights[FishRarity.Legendary] *= (1.0 + rarityBoost);
            }

            var totalRarityWeight = rarityWeights.Values.Sum();

            // Base star probabilities: 5% 3-star, 20% 2-star, 75% 1-star
            // Star boost increases both 2-star and 3-star chances
            var threeStarChance = 5.0 + (starBoost * 100);
            var twoStarChance = 20.0 + (starBoost * 100);
            var oneStarChance = 100.0 - threeStarChance - twoStarChance;

            var starProbabilities = new Dictionary<int, double>
            {
                { 1, oneStarChance / 100.0 },
                { 2, twoStarChance / 100.0 },
                { 3, threeStarChance / 100.0 }
            };

            // Weight boost multiplier (applied directly to weight calculation)
            var weightMultiplier = 1.0 + weightBoost;

            double expectedGold = 0.0;

            // For each rarity tier
            foreach (var (rarity, rarityWeight) in rarityWeights)
            {
                var fishOfRarity = fishTypes.Where(f => f.Rarity == rarity).ToList();
                if (!fishOfRarity.Any()) continue;

                var rarityProbability = rarityWeight / totalRarityWeight;
                var perFishProbability = rarityProbability / fishOfRarity.Count;

                // For each fish in this rarity
                foreach (var fish in fishOfRarity)
                {
                    // For each star level
                    foreach (var (stars, starProb) in starProbabilities)
                    {
                        // Calculate expected weight with boosts
                        var avgWeightMultiplier = (0.8 + 1.13) / 2.0;  // 0.965
                        var starWeightMultiplier = stars switch
                        {
                            3 => 1.5,
                            2 => 1.2,
                            _ => 1.0
                        };

                        var expectedWeight = fish.BaseWeight * avgWeightMultiplier * starWeightMultiplier * weightMultiplier;

                        // Calculate expected gold
                        var (minGoldMultiplier, maxGoldMultiplier) = stars switch
                        {
                            3 => (1.25, 1.41),
                            2 => (1.0, 1.25),
                            _ => (0.75, 1.0)
                        };

                        var avgGoldMultiplier = (minGoldMultiplier + maxGoldMultiplier) / 2.0;

                        // Weight influence on gold
                        var weightGoldMultiplier = 1.0;
                        if (fish.BaseWeight > 0)
                        {
                            var weightRatio = expectedWeight / fish.BaseWeight;
                            weightGoldMultiplier = 0.9 + ((weightRatio - 0.8) / (1.13 - 0.8) * 0.165);
                            weightGoldMultiplier = Math.Max(0.9, Math.Min(1.065, weightGoldMultiplier));
                        }

                        var gold = fish.BaseGold * avgGoldMultiplier * weightGoldMultiplier;
                        gold = Math.Max(1, gold);

                        expectedGold += perFishProbability * starProb * gold;
                    }
                }
            }

            return expectedGold;
        }

        private class ProgressionTier
        {
            public string Name { get; set; } = string.Empty;
            public int Weeks { get; set; }
            public double RarityBoost { get; set; }
            public double StarBoost { get; set; }
            public double WeightBoost { get; set; }
        }
    }
}
