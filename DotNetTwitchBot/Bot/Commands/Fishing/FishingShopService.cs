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
    }
}
