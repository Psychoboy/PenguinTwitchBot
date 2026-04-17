using DotNetTwitchBot.Bot.Commands.Fishing;
using DotNetTwitchBot.Bot.Core.Database;
using DotNetTwitchBot.Bot.Models.Fishing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotNetTwitchBot.Test.Bot.Commands.Fishing
{
    public class FishingShopServiceTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly FishingShopService _shopService;
        private readonly ApplicationDbContext _context;
        private readonly string _databaseName;

        public FishingShopServiceTests()
        {
            _databaseName = $"FishingShopTestDb_{Guid.NewGuid()}";

            var services = new ServiceCollection();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
            services.AddLogging(builder => builder.AddConsole());

            _serviceProvider = services.BuildServiceProvider();
            _scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
            _context = _serviceProvider.GetRequiredService<ApplicationDbContext>();

            var logger = Substitute.For<ILogger<FishingShopService>>();
            _shopService = new FishingShopService(_scopeFactory, logger);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _serviceProvider.Dispose();
        }

        #region Shop Item CRUD Tests

        [Fact]
        public async Task GetAllShopItems_ReturnsAllItems()
        {
            // Arrange
            var items = new List<FishingShopItem>
            {
                new() { Id = 1, Name = "Basic Rod", Cost = 100, Enabled = true },
                new() { Id = 2, Name = "Advanced Rod", Cost = 500, Enabled = true },
                new() { Id = 3, Name = "Disabled Rod", Cost = 1000, Enabled = false }
            };
            _context.FishingShopItems.AddRange(items);
            await _context.SaveChangesAsync();

            // Act
            var result = await _shopService.GetAllShopItems();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public async Task GetShopItemById_ReturnsCorrectItem()
        {
            // Arrange
            var item = new FishingShopItem
            {
                Id = 1,
                Name = "Test Rod",
                Cost = 250,
                EquipmentSlot = EquipmentSlot.Rod,
                BoostType = FishingBoostType.GeneralRarityBoost,
                BoostAmount = 0.5,
                Enabled = true
            };
            _context.FishingShopItems.Add(item);
            await _context.SaveChangesAsync();

            // Act
            var result = await _shopService.GetShopItemById(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test Rod", result.Name);
            Assert.Equal(250, result.Cost);
            Assert.Equal(EquipmentSlot.Rod, result.EquipmentSlot);
        }

        [Fact]
        public async Task GetShopItemById_NonExistent_ReturnsNull()
        {
            // Act
            var result = await _shopService.GetShopItemById(999);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task AddShopItem_CreatesNewItem()
        {
            // Arrange
            var newItem = new FishingShopItem
            {
                Name = "Pro Reel",
                Cost = 350,
                EquipmentSlot = EquipmentSlot.Reel,
                BoostType = FishingBoostType.StarBoost,
                BoostAmount = 0.25,
                Enabled = true
            };

            // Act
            await _shopService.AddShopItem(newItem);

            // Assert
            var item = await _context.FishingShopItems.FirstOrDefaultAsync(i => i.Name == "Pro Reel");
            Assert.NotNull(item);
            Assert.Equal(350, item.Cost);
            Assert.Equal(EquipmentSlot.Reel, item.EquipmentSlot);
        }

        [Fact]
        public async Task UpdateShopItem_ModifiesExistingItem()
        {
            // Arrange
            var item = new FishingShopItem
            {
                Id = 1,
                Name = "Old Name",
                Cost = 100,
                Enabled = true
            };
            _context.FishingShopItems.Add(item);
            await _context.SaveChangesAsync();

            // Act
            item.Name = "New Name";
            item.Cost = 200;
            await _shopService.UpdateShopItem(item);

            // Assert
            var updated = await _context.FishingShopItems.FindAsync(1);
            Assert.NotNull(updated);
            Assert.Equal("New Name", updated.Name);
            Assert.Equal(200, updated.Cost);
        }

        [Fact]
        public async Task DeleteShopItem_RemovesItem()
        {
            // Arrange
            var item = new FishingShopItem
            {
                Id = 1,
                Name = "To Delete",
                Cost = 100,
                Enabled = true
            };
            _context.FishingShopItems.Add(item);
            await _context.SaveChangesAsync();

            // Act
            await _shopService.DeleteShopItem(1);

            // Assert - Use service method to check deletion
            var deleted = await _shopService.GetShopItemById(1);
            Assert.Null(deleted);
        }

        #endregion

        #region Price Adjustment Tests

        [Fact]
        public async Task ApplyPriceMultiplier_AllItems_AdjustsAllPrices()
        {
            // Arrange
            var items = new List<FishingShopItem>
            {
                new() { Id = 1, Name = "Item 1", Cost = 100, Enabled = true },
                new() { Id = 2, Name = "Item 2", Cost = 200, Enabled = true },
                new() { Id = 3, Name = "Item 3", Cost = 300, Enabled = true }
            };
            _context.FishingShopItems.AddRange(items);
            await _context.SaveChangesAsync();

            // Act
            var updatedCount = await _shopService.ApplyPriceMultiplier(2.0); // Double all prices

            // Assert
            Assert.Equal(3, updatedCount);
            var updated = await _shopService.GetAllShopItems();
            Assert.Equal(200, updated.First(i => i.Name == "Item 1").Cost);
            Assert.Equal(400, updated.First(i => i.Name == "Item 2").Cost);
            Assert.Equal(600, updated.First(i => i.Name == "Item 3").Cost);
        }

        [Fact]
        public async Task ApplyPriceMultiplier_WithSlotFilter_AdjustsOnlyMatchingSlot()
        {
            // Arrange
            var items = new List<FishingShopItem>
            {
                new() { Id = 1, Name = "Rod", Cost = 100, EquipmentSlot = EquipmentSlot.Rod, Enabled = true },
                new() { Id = 2, Name = "Reel", Cost = 200, EquipmentSlot = EquipmentSlot.Reel, Enabled = true },
                new() { Id = 3, Name = "Hook", Cost = 300, EquipmentSlot = EquipmentSlot.Hook, Enabled = true }
            };
            _context.FishingShopItems.AddRange(items);
            await _context.SaveChangesAsync();

            // Act
            var updatedCount = await _shopService.ApplyPriceMultiplier(2.0, slot: EquipmentSlot.Rod);

            // Assert
            Assert.Equal(1, updatedCount);
            var updated = await _shopService.GetAllShopItems();
            Assert.Equal(200, updated.First(i => i.Name == "Rod").Cost); // Rod doubled
            Assert.Equal(200, updated.First(i => i.Name == "Reel").Cost); // Reel unchanged
            Assert.Equal(300, updated.First(i => i.Name == "Hook").Cost); // Hook unchanged
        }

        [Fact]
        public async Task UpdateShopItemPrices_ByName_UpdatesCorrectItems()
        {
            // Arrange
            var items = new List<FishingShopItem>
            {
                new() { Id = 1, Name = "Basic Rod", Cost = 100, Enabled = true },
                new() { Id = 2, Name = "Advanced Rod", Cost = 500, Enabled = true },
                new() { Id = 3, Name = "Pro Rod", Cost = 1000, Enabled = true }
            };
            _context.FishingShopItems.AddRange(items);
            await _context.SaveChangesAsync();

            var priceUpdates = new Dictionary<string, int>
            {
                { "Basic Rod", 150 },
                { "Pro Rod", 1200 }
            };

            // Act
            var updatedCount = await _shopService.UpdateShopItemPrices(priceUpdates);

            // Assert
            Assert.Equal(2, updatedCount);
            var updated = await _shopService.GetAllShopItems();
            Assert.Equal(150, updated.First(i => i.Name == "Basic Rod").Cost);  // Updated
            Assert.Equal(500, updated.First(i => i.Name == "Advanced Rod").Cost);  // Unchanged
            Assert.Equal(1200, updated.First(i => i.Name == "Pro Rod").Cost); // Updated
        }

        #endregion

        #region Default Shop Generation Tests

        [Fact]
        public async Task GenerateDefaultShopItems_CreatesAllCategories()
        {
            // Act
            var itemCount = await _shopService.GenerateDefaultShopItems();

            // Assert
            Assert.True(itemCount > 0);
            var items = await _shopService.GetAllShopItems();
            Assert.NotEmpty(items);

            // Verify we have items in each equipment slot
            Assert.Contains(items, i => i.EquipmentSlot == EquipmentSlot.Rod);
            Assert.Contains(items, i => i.EquipmentSlot == EquipmentSlot.Reel);
            Assert.Contains(items, i => i.EquipmentSlot == EquipmentSlot.Line);
            Assert.Contains(items, i => i.EquipmentSlot == EquipmentSlot.Hook);
            Assert.Contains(items, i => i.EquipmentSlot == EquipmentSlot.TackleBox);
            Assert.Contains(items, i => i.EquipmentSlot == EquipmentSlot.Net);
            Assert.Contains(items, i => i.EquipmentSlot == EquipmentSlot.Bait);
            Assert.Contains(items, i => i.EquipmentSlot == EquipmentSlot.Lure);
        }

        [Fact]
        public async Task GenerateDefaultShopItems_WithFishTypes_CreatesSpecificBoosts()
        {
            // Arrange - Add rare fish types (specific items only created for Rare+)
            var fishTypes = new List<FishType>
            {
                new() { Id = 1, Name = "Rare Salmon", Rarity = FishRarity.Rare, Enabled = true, BaseWeight = 20, BaseGold = 200 },
                new() { Id = 2, Name = "Epic Tuna", Rarity = FishRarity.Epic, Enabled = true, BaseWeight = 50, BaseGold = 500 }
            };
            _context.FishTypes.AddRange(fishTypes);
            await _context.SaveChangesAsync();

            // Act
            var itemCount = await _shopService.GenerateDefaultShopItems();

            // Assert
            Assert.True(itemCount > 0);
            var items = await _shopService.GetAllShopItems();

            // Verify fish-specific items were created (GenerateDefaultShopItems creates items for Rare+ fish)
            Assert.Contains(items, i => i.Name.Contains("Salmon") && i.BoostType == FishingBoostType.SpecificFishBoost);
            Assert.Contains(items, i => i.Name.Contains("Tuna") && i.BoostType == FishingBoostType.SpecificFishBoost);
        }

        [Fact]
        public async Task GenerateDefaultShopItems_Idempotent_DoesNotDuplicate()
        {
            // Act - Run twice
            await _shopService.GenerateDefaultShopItems();
            var firstCount = await _context.FishingShopItems.CountAsync();

            await _shopService.GenerateDefaultShopItems();
            var secondCount = await _context.FishingShopItems.CountAsync();

            // Assert - Should not create duplicates
            Assert.Equal(firstCount, secondCount);
        }

        #endregion

        #region Boost Type Tests

        [Fact]
        public async Task ShopItems_SupportMultipleBoostTypes()
        {
            // Arrange
            var tackleBox = new FishingShopItem
            {
                Id = 1,
                Name = "Multi-Boost Tackle Box",
                Cost = 1000,
                EquipmentSlot = EquipmentSlot.TackleBox,
                BoostType = FishingBoostType.GeneralRarityBoost,
                BoostAmount = 0.3,
                BoostType2 = FishingBoostType.WeightBoost,
                BoostAmount2 = 0.25,
                BoostType3 = FishingBoostType.StarBoost,
                BoostAmount3 = 0.15,
                Enabled = true
            };
            _context.FishingShopItems.Add(tackleBox);
            await _context.SaveChangesAsync();

            // Act
            var retrieved = await _shopService.GetShopItemById(1);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(FishingBoostType.GeneralRarityBoost, retrieved.BoostType);
            Assert.Equal(0.3, retrieved.BoostAmount);
            Assert.Equal(FishingBoostType.WeightBoost, retrieved.BoostType2);
            Assert.Equal(0.25, retrieved.BoostAmount2);
            Assert.Equal(FishingBoostType.StarBoost, retrieved.BoostType3);
            Assert.Equal(0.15, retrieved.BoostAmount3);
        }

        [Fact]
        public async Task ShopItems_ConsumableWithMaxUses_StoresCorrectly()
        {
            // Arrange
            var bait = new FishingShopItem
            {
                Id = 1,
                Name = "Premium Bait",
                Cost = 50,
                IsConsumable = true,
                MaxUses = 5,
                BoostType = FishingBoostType.GeneralRarityBoost,
                BoostAmount = 0.5,
                Enabled = true
            };
            _context.FishingShopItems.Add(bait);
            await _context.SaveChangesAsync();

            // Act
            var retrieved = await _shopService.GetShopItemById(1);

            // Assert
            Assert.NotNull(retrieved);
            Assert.True(retrieved.IsConsumable);
            Assert.Equal(5, retrieved.MaxUses);
        }

        #endregion
    }
}
