using DotNetTwitchBot.Bot.Core.Database;
using DotNetTwitchBot.Bot.Models.Fishing;
using Microsoft.EntityFrameworkCore;

namespace DotNetTwitchBot.Bot.Commands.Fishing
{
    public class FishingShopService : IFishingShopService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<FishingShopService> _logger;

        public FishingShopService(IServiceScopeFactory scopeFactory, ILogger<FishingShopService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
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

        public async Task<int> UpdateShopItemPrices(Dictionary<string, int> priceUpdates)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var itemNames = priceUpdates.Keys.ToList();

            // Single query to get all items at once (avoids N+1 problem)
            var items = await context.FishingShopItems
                .Where(i => itemNames.Contains(i.Name))
                .ToListAsync();

            var updatedCount = 0;
            foreach (var item in items)
            {
                if (priceUpdates.TryGetValue(item.Name, out var newPrice) && item.Cost != newPrice)
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

        public async Task<int> GenerateDefaultShopItems()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var generator = new FishingShopItemGenerator();
            return await generator.GenerateDefaultItems(context);
        }

        /// <summary>
        /// Calculates dynamic tiers for all shop items based on price rankings within each equipment slot.
        /// Returns a dictionary mapping item ID to its calculated tier.
        /// </summary>
        public async Task<Dictionary<int, EquipmentTier>> CalculateDynamicTiers()
        {
            var allItems = await GetAllShopItems();
            return CalculateDynamicTiers(allItems);
        }

        /// <summary>
        /// Calculates dynamic tiers from pre-fetched shop items (avoids extra DB query).
        /// Pre-groups by slot to avoid O(n²) complexity.
        /// </summary>
        public Dictionary<int, EquipmentTier> CalculateDynamicTiers(List<FishingShopItem> allItems)
        {
            var tierMap = new Dictionary<int, EquipmentTier>();

            // Pre-group by equipment slot to avoid O(n²) - each slot is sorted once
            var itemsBySlot = allItems
                .Where(i => !i.IsConsumable && i.Enabled && i.EquipmentSlot.HasValue)
                .GroupBy(i => i.EquipmentSlot!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(i => i.Cost).ToList()
                );

            // Assign tiers within each slot group
            foreach (var (slot, itemsInSlot) in itemsBySlot)
            {
                var totalItems = itemsInSlot.Count;
                if (totalItems == 0) continue;

                // Calculate quartile cutoffs
                var topCutoff = Math.Max(1, (int)Math.Ceiling(totalItems * 0.25));
                var highCutoff = Math.Max(2, (int)Math.Ceiling(totalItems * 0.50));
                var midCutoff = Math.Max(3, (int)Math.Ceiling(totalItems * 0.75));

                for (int rank = 0; rank < totalItems; rank++)
                {
                    var item = itemsInSlot[rank];
                    var tier = rank < topCutoff ? EquipmentTier.Top
                             : rank < highCutoff ? EquipmentTier.High
                             : rank < midCutoff ? EquipmentTier.Mid
                             : EquipmentTier.Entry;

                    tierMap[item.Id] = tier;
                }
            }

            // Handle consumables
            foreach (var item in allItems.Where(i => i.IsConsumable))
            {
                tierMap[item.Id] = EquipmentTier.Consumable;
            }

            return tierMap;
        }

        /// <summary>
        /// Determines the equipment tier for an item based on its price rank within its slot category.
        /// - Highest price in slot = Top Tier
        /// - Then High Tier, Mid Tier, Entry Tier based on quartiles
        /// - Works dynamically even if a slot has 1-4 items
        /// </summary>
        public EquipmentTier GetDynamicTier(FishingShopItem item, List<FishingShopItem> allItems)
        {
            // Consumables always get Consumable tier
            if (item.IsConsumable) return EquipmentTier.Consumable;

            // Items without equipment slot get Entry tier by default
            if (!item.EquipmentSlot.HasValue) return EquipmentTier.Entry;

            // Get all permanent items in the same equipment slot, ordered by price (descending)
            var itemsInSlot = allItems
                .Where(i => i.EquipmentSlot == item.EquipmentSlot && !i.IsConsumable && i.Enabled)
                .OrderByDescending(i => i.Cost)
                .ToList();

            // If only one item in slot, it's Top Tier
            if (itemsInSlot.Count == 1) return EquipmentTier.Top;

            // Find the rank of current item (0-indexed, 0 = highest price)
            var rank = itemsInSlot.FindIndex(i => i.Id == item.Id);
            if (rank == -1) return EquipmentTier.Entry; // Not found, shouldn't happen

            // Divide into quartiles for tier assignment
            var totalItems = itemsInSlot.Count;

            // Top 25% = Top Tier (at least 1 item)
            // Next 25% = High Tier
            // Next 25% = Mid Tier  
            // Bottom 25% = Entry Tier

            var topCutoff = Math.Max(1, (int)Math.Ceiling(totalItems * 0.25));
            var highCutoff = Math.Max(2, (int)Math.Ceiling(totalItems * 0.50));
            var midCutoff = Math.Max(3, (int)Math.Ceiling(totalItems * 0.75));

            if (rank < topCutoff)
                return EquipmentTier.Top;
            else if (rank < highCutoff)
                return EquipmentTier.High;
            else if (rank < midCutoff)
                return EquipmentTier.Mid;
            else
                return EquipmentTier.Entry;
        }
    }
}
