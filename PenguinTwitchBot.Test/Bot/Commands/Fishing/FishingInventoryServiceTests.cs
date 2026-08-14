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
        public async Task ConsumeItemUses_UnequipsNonConsumableWhenDepleted()
        {
            var shopItem = new FishingShopItem { Id = 1, Name = "Rod", MaxUses = 1, IsConsumable = false };
            _context.FishingShopItems.Add(shopItem);
            var boost = new UserFishingBoost { Id = 1, UserId = "user1", ShopItemId = 1, IsEquipped = true, RemainingUses = 1 };
            _context.UserFishingBoosts.Add(boost);
            await _context.SaveChangesAsync();

            await _sut.ConsumeItemUses("user1", new[] { 1 });

            var updated = await _context.UserFishingBoosts.AsNoTracking().SingleAsync(b => b.Id == 1);
            Assert.Equal(0, updated.RemainingUses);
            Assert.False(updated.IsEquipped);
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

            var updated = await _context.UserFishingBoosts.AsNoTracking().SingleAsync(b => b.Id == 1);
            Assert.Equal(0, updated.RemainingUses);
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
    }
}
