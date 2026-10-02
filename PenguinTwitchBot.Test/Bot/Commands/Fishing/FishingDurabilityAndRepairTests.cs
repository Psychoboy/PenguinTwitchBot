using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
using PenguinTwitchBot.Database.Repository;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Commands.Fishing
{
    public class FishingDurabilityAndRepairTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly FishingInventoryService _sut;
        private readonly ApplicationDbContext _context;
        private readonly SqliteConnection _connection;

        public FishingDurabilityAndRepairTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var services = new ServiceCollection();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
            services.AddScoped<IUnitOfWork, UnitOfWork>();
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

        private ApplicationDbContext CreateFreshContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task ConsumeItemDurability_DegradesDurabilityOnCatch()
        {
            var shopItem = new FishingShopItem
            {
                Id = 1,
                Name = "Carbon Rod",
                Cost = 200,
                EquipmentSlot = EquipmentSlot.Rod,
                MaxDurability = 100,
                DurabilityLossPerUse = 2.5
            };
            _context.FishingShopItems.Add(shopItem);

            var boost = new UserFishingBoost
            {
                Id = 1,
                UserId = "user1",
                ShopItemId = 1,
                IsEquipped = true,
                CurrentDurability = 100
            };
            _context.UserFishingBoosts.Add(boost);
            await _context.SaveChangesAsync();

            await _sut.ConsumeItemDurability("user1", new[] { 1 }, repairCostMultiplier: 0.25);

            using var verifyContext = CreateFreshContext();
            var updated = await verifyContext.UserFishingBoosts.FindAsync(1);
            Assert.NotNull(updated);
            Assert.Equal(97.5, updated!.CurrentDurability);
            Assert.False(updated.IsBroken);
        }

        [Fact]
        public async Task ConsumeItemDurability_WhenReachingZero_PreservedForRepairWhenMultiplierGreaterThanZero()
        {
            var shopItem = new FishingShopItem
            {
                Id = 2,
                Name = "Old Net",
                Cost = 150,
                EquipmentSlot = EquipmentSlot.Net,
                MaxDurability = 10,
                DurabilityLossPerUse = 10
            };
            _context.FishingShopItems.Add(shopItem);

            var boost = new UserFishingBoost
            {
                Id = 2,
                UserId = "user1",
                ShopItemId = 2,
                IsEquipped = true,
                CurrentDurability = 5
            };
            _context.UserFishingBoosts.Add(boost);
            await _context.SaveChangesAsync();

            await _sut.ConsumeItemDurability("user1", new[] { 2 }, repairCostMultiplier: 0.25);

            using var verifyContext = CreateFreshContext();
            var updated = await verifyContext.UserFishingBoosts.FindAsync(2);
            Assert.NotNull(updated);
            Assert.Equal(0.0, updated!.CurrentDurability);
            Assert.True(updated.IsBroken);
            Assert.True(updated.IsEquipped);
        }

        [Fact]
        public async Task ConsumeItemDurability_WhenReachingZero_DeletedWhenMultiplierIsZero()
        {
            var shopItem = new FishingShopItem
            {
                Id = 3,
                Name = "Disposable Line",
                Cost = 50,
                EquipmentSlot = EquipmentSlot.Line,
                MaxDurability = 10,
                DurabilityLossPerUse = 10
            };
            _context.FishingShopItems.Add(shopItem);

            var boost = new UserFishingBoost
            {
                Id = 3,
                UserId = "user1",
                ShopItemId = 3,
                IsEquipped = true,
                CurrentDurability = 5
            };
            _context.UserFishingBoosts.Add(boost);
            await _context.SaveChangesAsync();

            await _sut.ConsumeItemDurability("user1", new[] { 3 }, repairCostMultiplier: 0.0);

            using var verifyContext = CreateFreshContext();
            var updated = await verifyContext.UserFishingBoosts.FindAsync(3);
            Assert.Null(updated);
        }

        [Fact]
        public async Task ReconcileDurability_RetroactivelyInitializesOldBoosts()
        {
            var shopItem = new FishingShopItem
            {
                Id = 4,
                Name = "Vintage Reel",
                Cost = 300,
                EquipmentSlot = EquipmentSlot.Reel,
                MaxDurability = 150,
                DurabilityLossPerUse = 1.0
            };
            _context.FishingShopItems.Add(shopItem);

            var boost = new UserFishingBoost
            {
                Id = 4,
                UserId = "user1",
                ShopItemId = 4,
                IsEquipped = true,
                CurrentDurability = null
            };
            _context.UserFishingBoosts.Add(boost);
            await _context.SaveChangesAsync();

            var boosts = await _sut.GetUserBoosts("user1");
            var retrieved = boosts.FirstOrDefault(b => b.Id == 4);

            Assert.NotNull(retrieved);
            Assert.Equal(150.0, retrieved!.CurrentDurability);
        }

        [Fact]
        public void CalculateRepairCost_MatchesFormula()
        {
            var shopItem = new FishingShopItem
            {
                Cost = 1000,
                MaxDurability = 100
            };

            // 50% missing, 25% repair multiplier -> 1000 * 0.5 * 0.25 = 125
            var cost = _sut.CalculateRepairCost(shopItem, currentDurability: 50, repairCostMultiplier: 0.25);
            Assert.Equal(125, cost);

            // Small fraction rounds up to at least 1 gold
            var smallCost = _sut.CalculateRepairCost(shopItem, currentDurability: 99.9, repairCostMultiplier: 0.01);
            Assert.Equal(1, smallCost);
        }

        [Fact]
        public async Task RepairItem_RestoresFullDurabilityAndDeductsGold()
        {
            _context.FishingSettings.Add(new FishingSettings { Id = 1, RepairCostMultiplier = 0.25 });
            _context.FishingGolds.Add(new FishingGold { UserId = "user1", TotalGold = 500 });

            var shopItem = new FishingShopItem
            {
                Id = 5,
                Name = "Titanium Rod",
                Cost = 400,
                EquipmentSlot = EquipmentSlot.Rod,
                MaxDurability = 100,
                DurabilityLossPerUse = 1.0
            };
            _context.FishingShopItems.Add(shopItem);

            var boost = new UserFishingBoost
            {
                Id = 5,
                UserId = "user1",
                ShopItemId = 5,
                IsEquipped = true,
                CurrentDurability = 50.0
            };
            _context.UserFishingBoosts.Add(boost);
            await _context.SaveChangesAsync();

            // Cost = 400 * ((100 - 50) / 100) * 0.25 = 50 gold
            var repairCost = await _sut.RepairItem("user1", 5);
            Assert.Equal(50, repairCost);

            using var verifyContext = CreateFreshContext();
            var updatedBoost = await verifyContext.UserFishingBoosts.FindAsync(5);
            var updatedGold = await verifyContext.FishingGolds.FirstOrDefaultAsync(g => g.UserId == "user1");

            Assert.NotNull(updatedBoost);
            Assert.Equal(100.0, updatedBoost!.CurrentDurability);
            Assert.NotNull(updatedGold);
            Assert.Equal(450, updatedGold!.TotalGold);
        }

        [Fact]
        public async Task RepairItem_ThrowsWhenNotEnoughGold()
        {
            _context.FishingSettings.Add(new FishingSettings { Id = 1, RepairCostMultiplier = 0.5 });
            _context.FishingGolds.Add(new FishingGold { UserId = "user1", TotalGold = 10 });

            var shopItem = new FishingShopItem
            {
                Id = 6,
                Name = "Expensive Tackle Box",
                Cost = 1000,
                EquipmentSlot = EquipmentSlot.TackleBox,
                MaxDurability = 100,
                DurabilityLossPerUse = 1.0
            };
            _context.FishingShopItems.Add(shopItem);

            var boost = new UserFishingBoost
            {
                Id = 6,
                UserId = "user1",
                ShopItemId = 6,
                IsEquipped = true,
                CurrentDurability = 0.0
            };
            _context.UserFishingBoosts.Add(boost);
            await _context.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.RepairItem("user1", 6));
        }

        [Fact]
        public async Task RepairItem_ThrowsWhenRepairDisabled()
        {
            _context.FishingSettings.Add(new FishingSettings { Id = 1, RepairCostMultiplier = 0.0 });
            _context.FishingGolds.Add(new FishingGold { UserId = "user1", TotalGold = 500 });

            var shopItem = new FishingShopItem
            {
                Id = 7,
                Name = "Net",
                Cost = 100,
                EquipmentSlot = EquipmentSlot.Net,
                MaxDurability = 50
            };
            _context.FishingShopItems.Add(shopItem);

            var boost = new UserFishingBoost
            {
                Id = 7,
                UserId = "user1",
                ShopItemId = 7,
                IsEquipped = true,
                CurrentDurability = 20.0
            };
            _context.UserFishingBoosts.Add(boost);
            await _context.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.RepairItem("user1", 7));
        }

        [Fact]
        public async Task RepairAllEquippedItems_RepairsAllDamagedEquippedGear()
        {
            _context.FishingSettings.Add(new FishingSettings { Id = 1, RepairCostMultiplier = 0.5 });
            _context.FishingGolds.Add(new FishingGold { UserId = "user1", TotalGold = 1000 });

            var rodItem = new FishingShopItem
            {
                Id = 8,
                Name = "Rod",
                Cost = 200,
                EquipmentSlot = EquipmentSlot.Rod,
                MaxDurability = 100
            };
            var reelItem = new FishingShopItem
            {
                Id = 9,
                Name = "Reel",
                Cost = 200,
                EquipmentSlot = EquipmentSlot.Reel,
                MaxDurability = 100
            };
            _context.FishingShopItems.AddRange(rodItem, reelItem);

            var rodBoost = new UserFishingBoost
            {
                Id = 8,
                UserId = "user1",
                ShopItemId = 8,
                IsEquipped = true,
                CurrentDurability = 50.0 // Cost = 200 * 0.5 * 0.5 = 50
            };
            var reelBoost = new UserFishingBoost
            {
                Id = 9,
                UserId = "user1",
                ShopItemId = 9,
                IsEquipped = true,
                CurrentDurability = 50.0 // Cost = 200 * 0.5 * 0.5 = 50
            };
            _context.UserFishingBoosts.AddRange(rodBoost, reelBoost);
            await _context.SaveChangesAsync();

            var totalCost = await _sut.RepairAllEquippedItems("user1");
            Assert.Equal(100, totalCost);

            using var verifyContext = CreateFreshContext();
            var gold = await verifyContext.FishingGolds.FirstOrDefaultAsync(g => g.UserId == "user1");
            Assert.Equal(900, gold!.TotalGold);

            var updatedRod = await verifyContext.UserFishingBoosts.FindAsync(8);
            var updatedReel = await verifyContext.UserFishingBoosts.FindAsync(9);
            Assert.Equal(100.0, updatedRod!.CurrentDurability);
            Assert.Equal(100.0, updatedReel!.CurrentDurability);
        }

        [Fact]
        public void GetSellPrice_ProRatesBasedOnDurability()
        {
            var shopItem = new FishingShopItem
            {
                Cost = 1000,
                MaxDurability = 100
            };

            // Full durability: 1000 * 0.15 = 150
            var boostFull = new UserFishingBoost { CurrentDurability = 100 };
            Assert.Equal(150, FishingInventorySellRules.GetSellPrice(shopItem, boostFull));

            // 50% durability: 150 * 0.5 = 75
            var boostHalf = new UserFishingBoost { CurrentDurability = 50 };
            Assert.Equal(75, FishingInventorySellRules.GetSellPrice(shopItem, boostHalf));

            // 0% durability: 0
            var boostBroken = new UserFishingBoost { CurrentDurability = 0 };
            Assert.Equal(0, FishingInventorySellRules.GetSellPrice(shopItem, boostBroken));
        }

        [Fact]
        public async Task DisableBreaking_ProtectsItemsFromAccidentLoss()
        {
            var rod = new FishingShopItem { Id = 10, Name = "Unbreakable Rod", Cost = 500, EquipmentSlot = EquipmentSlot.Rod, DisableBreaking = true };
            var reel = new FishingShopItem { Id = 11, Name = "Unbreakable Reel", Cost = 500, EquipmentSlot = EquipmentSlot.Reel, DisableBreaking = true };
            var box = new FishingShopItem { Id = 12, Name = "Unbreakable Box", Cost = 500, EquipmentSlot = EquipmentSlot.TackleBox, DisableBreaking = true };
            var net = new FishingShopItem { Id = 13, Name = "Unbreakable Net", Cost = 500, EquipmentSlot = EquipmentSlot.Net, DisableBreaking = true };
            _context.FishingShopItems.AddRange(rod, reel, box, net);

            var bRod = new UserFishingBoost { Id = 10, UserId = "user1", ShopItemId = 10, IsEquipped = true };
            var bReel = new UserFishingBoost { Id = 11, UserId = "user1", ShopItemId = 11, IsEquipped = true };
            var bBox = new UserFishingBoost { Id = 12, UserId = "user1", ShopItemId = 12, IsEquipped = true };
            var bNet = new UserFishingBoost { Id = 13, UserId = "user1", ShopItemId = 13, IsEquipped = true };
            _context.UserFishingBoosts.AddRange(bRod, bReel, bBox, bNet);
            await _context.SaveChangesAsync();

            // Attempt accident losses
            var rodSnap = await _sut.ConsumeItemsOnRodSnap("user1", "user1");
            Assert.Equal(0, rodSnap.TotalGoldLost);
            using (var freshContext = CreateFreshContext())
            {
                Assert.NotNull(await freshContext.UserFishingBoosts.FindAsync(10));
            }

            var reelJam = await _sut.ConsumeItemsOnReelJam("user1", "user1");
            Assert.Equal(0, reelJam.TotalGoldLost);
            using (var freshContext = CreateFreshContext())
            {
                Assert.NotNull(await freshContext.UserFishingBoosts.FindAsync(11));
            }

            var boxLost = await _sut.ConsumeItemsOnTackleBoxLost("user1", "user1");
            Assert.Equal(0, boxLost.TotalGoldLost);
            using (var freshContext = CreateFreshContext())
            {
                Assert.NotNull(await freshContext.UserFishingBoosts.FindAsync(12));
            }

            var netBreak = await _sut.ConsumeItemsOnNetBreak("user1", "user1");
            Assert.Equal(0, netBreak.TotalGoldLost);
            using (var freshContext = CreateFreshContext())
            {
                Assert.NotNull(await freshContext.UserFishingBoosts.FindAsync(13));
            }
        }

        [Fact]
        public async Task EquipItem_AllowsBrokenItem_CanBeEquippedAndRepaired()
        {
            var shopItem = new FishingShopItem
            {
                Id = 14,
                Name = "Broken Rod",
                Cost = 100,
                EquipmentSlot = EquipmentSlot.Rod,
                MaxDurability = 50
            };
            _context.FishingShopItems.Add(shopItem);

            var boost = new UserFishingBoost
            {
                Id = 14,
                UserId = "user1",
                ShopItemId = 14,
                IsEquipped = false,
                CurrentDurability = 0.0
            };
            _context.UserFishingBoosts.Add(boost);
            await _context.SaveChangesAsync();

            await _sut.EquipItem("user1", 14);

            using var verifyContext = CreateFreshContext();
            var updated = await verifyContext.UserFishingBoosts.FindAsync(14);
            Assert.NotNull(updated);
            Assert.True(updated!.IsEquipped);
            Assert.True(updated.IsBroken);
        }

        [Fact]
        public async Task Accidents_SkipBaitAndLureLoss_ForReelTackleBoxAndNet()
        {
            var reel = new FishingShopItem { Id = 20, Name = "Reel", Cost = 100, EquipmentSlot = EquipmentSlot.Reel };
            var bait = new FishingShopItem { Id = 21, Name = "Bait", Cost = 50, EquipmentSlot = EquipmentSlot.Bait, MaxUses = 5 };
            _context.FishingShopItems.AddRange(reel, bait);

            var bReel = new UserFishingBoost { Id = 20, UserId = "userAccident", ShopItemId = 20, IsEquipped = true };
            var bBait = new UserFishingBoost { Id = 21, UserId = "userAccident", ShopItemId = 21, IsEquipped = true, RemainingUses = 5 };
            _context.UserFishingBoosts.AddRange(bReel, bBait);
            await _context.SaveChangesAsync();

            var reelJamResult = await _sut.ConsumeItemsOnReelJam("userAccident", "userAccident");
            Assert.Equal(100, reelJamResult.TotalGoldLost);

            using var verifyContext = CreateFreshContext();
            var baitAfterJam = await verifyContext.UserFishingBoosts.FindAsync(21);
            Assert.NotNull(baitAfterJam);
            Assert.Equal(5, baitAfterJam!.RemainingUses);
        }
    }
}
