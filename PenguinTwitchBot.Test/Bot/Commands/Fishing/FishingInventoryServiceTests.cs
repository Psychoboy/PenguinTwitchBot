using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Commands.Fishing
{
    // Uses a real Sqlite connection (not the EF InMemory provider) because ConsumeItemUses
    // relies on ExecuteUpdateAsync, which the InMemory provider does not support.
    public class FishingInventoryServiceTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly FishingInventoryService _sut;
        private readonly ApplicationDbContext _context;
        private readonly SqliteConnection _connection;

        public FishingInventoryServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var services = new ServiceCollection();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
            services.AddLogging(builder => builder.AddConsole());

            _serviceProvider = services.BuildServiceProvider();
            _scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
            _context = _serviceProvider.GetRequiredService<ApplicationDbContext>();
            _context.Database.EnsureCreated();

            var logger = Substitute.For<ILogger<FishingInventoryService>>();
            _sut = new FishingInventoryService(_scopeFactory, logger);
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
            _serviceProvider.Dispose();
        }

        [Fact]
        public async Task ConsumeItemUses_DecrementsFiniteUseItem()
        {
            var shopItem = new FishingShopItem { Id = 1, Name = "Bait", MaxUses = 5, IsConsumable = true };
            _context.FishingShopItems.Add(shopItem);
            var boost = new UserFishingBoost { Id = 1, UserId = "user1", ShopItemId = 1, IsEquipped = true, RemainingUses = 5 };
            _context.UserFishingBoosts.Add(boost);
            await _context.SaveChangesAsync();

            await _sut.ConsumeItemUses("user1", new[] { 1 });

            var updated = await _context.UserFishingBoosts.AsNoTracking().SingleAsync(b => b.Id == 1);
            Assert.Equal(4, updated.RemainingUses);
            Assert.True(updated.IsEquipped);
        }

        [Fact]
        public async Task ConsumeItemUses_RemovesConsumableWhenDepleted()
        {
            var shopItem = new FishingShopItem { Id = 1, Name = "Bait", MaxUses = 1, IsConsumable = true };
            _context.FishingShopItems.Add(shopItem);
            var boost = new UserFishingBoost { Id = 1, UserId = "user1", ShopItemId = 1, IsEquipped = true, RemainingUses = 1 };
            _context.UserFishingBoosts.Add(boost);
            await _context.SaveChangesAsync();

            await _sut.ConsumeItemUses("user1", new[] { 1 });

            var remaining = await _context.UserFishingBoosts.AsNoTracking().SingleOrDefaultAsync(b => b.Id == 1);
            Assert.Null(remaining);
        }

        [Fact]
        public async Task ConsumeItemUses_RemovesLimitedUseItemWhenDepleted()
        {
            var shopItem = new FishingShopItem { Id = 1, Name = "Rod", MaxUses = 1, IsConsumable = false };
            _context.FishingShopItems.Add(shopItem);
            var boost = new UserFishingBoost { Id = 1, UserId = "user1", ShopItemId = 1, IsEquipped = true, RemainingUses = 1 };
            _context.UserFishingBoosts.Add(boost);
            await _context.SaveChangesAsync();

            await _sut.ConsumeItemUses("user1", new[] { 1 });

            var remaining = await _context.UserFishingBoosts.AsNoTracking().SingleOrDefaultAsync(b => b.Id == 1);
            Assert.Null(remaining);
        }

        [Fact]
        public async Task ConsumeItemUses_EquipsNextAvailableCopyWhenDepleted()
        {
            var shopItem = new FishingShopItem { Id = 1, Name = "Bait", MaxUses = 1, IsConsumable = false };
            _context.FishingShopItems.Add(shopItem);
            _context.UserFishingBoosts.AddRange(
                new UserFishingBoost { Id = 1, UserId = "user1", ShopItemId = 1, IsEquipped = true, RemainingUses = 1 },
                new UserFishingBoost { Id = 2, UserId = "user1", ShopItemId = 1, IsEquipped = false, RemainingUses = 1 });
            await _context.SaveChangesAsync();

            await _sut.ConsumeItemUses("user1", new[] { 1 });

            var replacement = await _context.UserFishingBoosts.AsNoTracking().SingleAsync(b => b.Id == 2);
            Assert.True(replacement.IsEquipped);
        }

        [Fact]
        public async Task ConsumeItemUses_SerializesConcurrentCallsForSameUser()
        {
            var shopItem = new FishingShopItem { Id = 1, Name = "Bait", MaxUses = 5, IsConsumable = false };
            _context.FishingShopItems.Add(shopItem);
            var boost = new UserFishingBoost { Id = 1, UserId = "user1", ShopItemId = 1, IsEquipped = true, RemainingUses = 2 };
            _context.UserFishingBoosts.Add(boost);
            await _context.SaveChangesAsync();

            var first = _sut.ConsumeItemUses("user1", new[] { 1 });
            var second = _sut.ConsumeItemUses("user1", new[] { 1 });
            await Task.WhenAll(first, second);

            var updated = await _context.UserFishingBoosts.AsNoTracking().SingleOrDefaultAsync(b => b.Id == 1);
            Assert.Null(updated);
        }

        [Fact]
        public async Task ConsumeItemUses_LeavesUnlimitedUseItemUnchanged()
        {
            var shopItem = new FishingShopItem { Id = 1, Name = "Permanent Rod", IsConsumable = false };
            _context.FishingShopItems.Add(shopItem);
            var boost = new UserFishingBoost { Id = 1, UserId = "user1", ShopItemId = 1, IsEquipped = true, RemainingUses = -1 };
            _context.UserFishingBoosts.Add(boost);
            await _context.SaveChangesAsync();

            await _sut.ConsumeItemUses("user1", new[] { 1 });

            var updated = await _context.UserFishingBoosts.AsNoTracking().SingleAsync(b => b.Id == 1);
            Assert.Equal(-1, updated.RemainingUses);
            Assert.True(updated.IsEquipped);
        }

        [Fact]
        public async Task PurchaseBoost_CreatesMultipleLimitedUseItems()
        {
            _context.FishingShopItems.Add(new FishingShopItem
            {
                Id = 1,
                Name = "Bait",
                Cost = 100,
                MaxUses = 5,
                Enabled = true
            });
            _context.FishingGolds.Add(new FishingGold { UserId = "user1", TotalGold = 500 });
            await _context.SaveChangesAsync();

            await _sut.PurchaseBoost("user1", 1, 3);

            var items = await _context.UserFishingBoosts.AsNoTracking().Where(b => b.UserId == "user1").ToListAsync();
            var gold = await _context.FishingGolds.AsNoTracking().SingleAsync(g => g.UserId == "user1");
            Assert.Equal(3, items.Count);
            Assert.All(items, item => Assert.Equal(5, item.RemainingUses));
            Assert.Equal(200, gold.TotalGold);
        }
    }
}
