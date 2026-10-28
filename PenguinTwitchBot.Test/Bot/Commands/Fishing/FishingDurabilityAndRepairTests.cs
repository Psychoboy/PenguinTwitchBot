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
            services.AddLogging(builder => builder.AddConsole().AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None));

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
            var shopItemId = Guid.CreateVersion7();
            var boostId = Guid.CreateVersion7();
            var shopItem = new FishingShopItem
            {
                Id = shopItemId,
                Name = "Carbon Rod",
                Cost = 200,
                EquipmentSlot = EquipmentSlot.Rod,
                MaxDurability = 100,
                DurabilityLossPerUse = 2.5
            };
            _context.FishingShopItems.Add(shopItem);

            var boost = new UserFishingBoost
            {
                Id = boostId,
                UserId = "user1",
                ShopItemId = shopItemId,
                IsEquipped = true,
                CurrentDurability = 100
            };
            _context.UserFishingBoosts.Add(boost);
            await _context.SaveChangesAsync();

            await _sut.ConsumeItemDurability("user1", new[] { boostId }, repairCostMultiplier: 0.25);

            using var verifyContext = CreateFreshContext();
            var updated = await verifyContext.UserFishingBoosts.FindAsync(boostId);
            Assert.NotNull(updated);
            Assert.Equal(97.5, updated!.CurrentDurability);
            Assert.False(updated.IsBroken);
        }

        [Fact]
        public async Task ConsumeItemDurability_WhenReachingZero_PreservedForRepairWhenMultiplierGreaterThanZero()
        {
            var shopItemId = Guid.CreateVersion7();
            var boostId = Guid.CreateVersion7();
            var shopItem = new FishingShopItem
            {
                Id = shopItemId,
                Name = "Old Net",
                Cost = 150,
                EquipmentSlot = EquipmentSlot.Net,
                MaxDurability = 10,
                DurabilityLossPerUse = 10
            };
            _context.FishingShopItems.Add(shopItem);

            var boost = new UserFishingBoost
            {
                Id = boostId,
                UserId = "user1",
                ShopItemId = shopItemId,
                IsEquipped = true,
                CurrentDurability = 5
            };
            _context.UserFishingBoosts.Add(boost);
            await _context.SaveChangesAsync();

            await _sut.ConsumeItemDurability("user1", new[] { boostId }, repairCostMultiplier: 0.25);

            using var verifyContext = CreateFreshContext();
            var updated = await verifyContext.UserFishingBoosts.FindAsync(boostId);
            Assert.NotNull(updated);
            Assert.Equal(0.0, updated!.CurrentDurability);
            Assert.True(updated.IsBroken);
            Assert.True(updated.IsEquipped);
        }

        [Fact]
        public async Task ConsumeItemDurability_WhenReachingZero_DeletedWhenMultiplierIsZero()
        {
            var shopItemId = Guid.CreateVersion7();
            var boostId = Guid.CreateVersion7();
            var shopItem = new FishingShopItem
            {
                Id = shopItemId,
                Name = "Disposable Line",
                Cost = 50,
                EquipmentSlot = EquipmentSlot.Line,
                MaxDurability = 10,
                DurabilityLossPerUse = 10
            };
            _context.FishingShopItems.Add(shopItem);

            var boost = new UserFishingBoost
            {
                Id = boostId,
                UserId = "user1",
                ShopItemId = shopItemId,
                IsEquipped = true,
                CurrentDurability = 5
            };
            _context.UserFishingBoosts.Add(boost);
            await _context.SaveChangesAsync();

            await _sut.ConsumeItemDurability("user1", new[] { boostId }, repairCostMultiplier: 0.0);

            using var verifyContext = CreateFreshContext();
            var updated = await verifyContext.UserFishingBoosts.FindAsync(boostId);
            Assert.Null(updated);
        }

        [Fact]
        public async Task ReconcileDurability_RetroactivelyInitializesOldBoosts()
        {
            var shopItemId = Guid.CreateVersion7();
            var boostId = Guid.CreateVersion7();
            var shopItem = new FishingShopItem
            {
                Id = shopItemId,
                Name = "Vintage Reel",
                Cost = 300,
                EquipmentSlot = EquipmentSlot.Reel,
                MaxDurability = 150,
                DurabilityLossPerUse = 1.0
            };
            _context.FishingShopItems.Add(shopItem);

            var boost = new UserFishingBoost
            {
                Id = boostId,
                UserId = "user1",
                ShopItemId = shopItemId,
                IsEquipped = true,
                CurrentDurability = null
            };
            _context.UserFishingBoosts.Add(boost);
            await _context.SaveChangesAsync();

            var boosts = await _sut.GetUserBoosts("user1");
            var retrieved = boosts.FirstOrDefault(b => b.Id == boostId);

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
            _context.FishingSettings.Add(new FishingSettings { Id = Guid.CreateVersion7(), RepairCostMultiplier = 0.25 });
            _context.FishingGolds.Add(new FishingGold { UserId = "user1", TotalGold = 500 });

            var shopItemId = Guid.CreateVersion7();
            var boostId = Guid.CreateVersion7();
            var shopItem = new FishingShopItem
            {
                Id = shopItemId,
                Name = "Titanium Rod",
                Cost = 400,
                EquipmentSlot = EquipmentSlot.Rod,
                MaxDurability = 100,
                DurabilityLossPerUse = 1.0
            };
            _context.FishingShopItems.Add(shopItem);

            var boost = new UserFishingBoost
            {
                Id = boostId,
                UserId = "user1",
                ShopItemId = shopItemId,
                IsEquipped = true,
                CurrentDurability = 50.0
            };
            _context.UserFishingBoosts.Add(boost);
            await _context.SaveChangesAsync();

            // Cost = 400 * ((100 - 50) / 100) * 0.25 = 50 gold
            var repairCost = await _sut.RepairItem("user1", boostId);
            Assert.Equal(50, repairCost);

            using var verifyContext = CreateFreshContext();
            var updatedBoost = await verifyContext.UserFishingBoosts.FindAsync(boostId);
            var updatedGold = await verifyContext.FishingGolds.FirstOrDefaultAsync(g => g.UserId == "user1");

            Assert.NotNull(updatedBoost);
            Assert.Equal(100.0, updatedBoost!.CurrentDurability);
            Assert.NotNull(updatedGold);
            Assert.Equal(450, updatedGold!.TotalGold);

            var repairEvent = await verifyContext.FishingRepairEvents.FirstOrDefaultAsync();
            Assert.NotNull(repairEvent);
            Assert.Equal("user1", repairEvent!.UserId);
            Assert.Equal("Titanium Rod", repairEvent.ItemName);
            Assert.Equal("Rod", repairEvent.EquipmentSlot);
            Assert.Equal(50.0, repairEvent.DurabilityRestored);
            Assert.Equal(50m, repairEvent.GoldPaid);
            Assert.Equal("Single", repairEvent.RepairType);
        }

        [Fact]
        public async Task RepairItem_ThrowsWhenNotEnoughGold()
        {
            _context.FishingSettings.Add(new FishingSettings { Id = Guid.CreateVersion7(), RepairCostMultiplier = 0.5 });
            _context.FishingGolds.Add(new FishingGold { UserId = "user1", TotalGold = 10 });

            var shopItemId = Guid.CreateVersion7();
            var boostId = Guid.CreateVersion7();
            var shopItem = new FishingShopItem
            {
                Id = shopItemId,
                Name = "Expensive Tackle Box",
                Cost = 1000,
                EquipmentSlot = EquipmentSlot.TackleBox,
                MaxDurability = 100,
                DurabilityLossPerUse = 1.0
            };
            _context.FishingShopItems.Add(shopItem);

            var boost = new UserFishingBoost
            {
                Id = boostId,
                UserId = "user1",
                ShopItemId = shopItemId,
                IsEquipped = true,
                CurrentDurability = 0.0
            };
            _context.UserFishingBoosts.Add(boost);
            await _context.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.RepairItem("user1", boostId));
        }

        [Fact]
        public async Task RepairItem_ThrowsWhenRepairDisabled()
        {
            _context.FishingSettings.Add(new FishingSettings { Id = Guid.CreateVersion7(), RepairCostMultiplier = 0.0 });
            _context.FishingGolds.Add(new FishingGold { UserId = "user1", TotalGold = 500 });

            var shopItemId = Guid.CreateVersion7();
            var boostId = Guid.CreateVersion7();
            var shopItem = new FishingShopItem
            {
                Id = shopItemId,
                Name = "Net",
                Cost = 100,
                EquipmentSlot = EquipmentSlot.Net,
                MaxDurability = 50
            };
            _context.FishingShopItems.Add(shopItem);

            var boost = new UserFishingBoost
            {
                Id = boostId,
                UserId = "user1",
                ShopItemId = shopItemId,
                IsEquipped = true,
                CurrentDurability = 20.0
            };
            _context.UserFishingBoosts.Add(boost);
            await _context.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.RepairItem("user1", boostId));
        }

        [Fact]
        public async Task RepairAllEquippedItems_RepairsAllDamagedEquippedGear()
        {
            _context.FishingSettings.Add(new FishingSettings { Id = Guid.CreateVersion7(), RepairCostMultiplier = 0.5 });
            _context.FishingGolds.Add(new FishingGold { UserId = "user1", TotalGold = 1000 });

            var rodShopId = Guid.CreateVersion7();
            var reelShopId = Guid.CreateVersion7();
            var rodItem = new FishingShopItem
            {
                Id = rodShopId,
                Name = "Rod",
                Cost = 200,
                EquipmentSlot = EquipmentSlot.Rod,
                MaxDurability = 100
            };
            var reelItem = new FishingShopItem
            {
                Id = reelShopId,
                Name = "Reel",
                Cost = 200,
                EquipmentSlot = EquipmentSlot.Reel,
                MaxDurability = 100
            };
            _context.FishingShopItems.AddRange(rodItem, reelItem);

            var rodBoostId = Guid.CreateVersion7();
            var reelBoostId = Guid.CreateVersion7();
            var rodBoost = new UserFishingBoost
            {
                Id = rodBoostId,
                UserId = "user1",
                ShopItemId = rodShopId,
                IsEquipped = true,
                CurrentDurability = 50.0 // Cost = 200 * 0.5 * 0.5 = 50
            };
            var reelBoost = new UserFishingBoost
            {
                Id = reelBoostId,
                UserId = "user1",
                ShopItemId = reelShopId,
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

            var updatedRod = await verifyContext.UserFishingBoosts.FindAsync(rodBoostId);
            var updatedReel = await verifyContext.UserFishingBoosts.FindAsync(reelBoostId);
            Assert.Equal(100.0, updatedRod!.CurrentDurability);
            Assert.Equal(100.0, updatedReel!.CurrentDurability);

            var repairEvents = await verifyContext.FishingRepairEvents.ToListAsync();
            Assert.Equal(2, repairEvents.Count);
            Assert.All(repairEvents, r => Assert.Equal("AllEquipped", r.RepairType));
            var rodEvent = repairEvents.Single(r => r.EquipmentSlot == "Rod");
            var reelEvent = repairEvents.Single(r => r.EquipmentSlot == "Reel");
            Assert.Equal(50m, rodEvent.GoldPaid);
            Assert.Equal(50m, reelEvent.GoldPaid);
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
            var rodId = Guid.CreateVersion7();
            var reelId = Guid.CreateVersion7();
            var boxId = Guid.CreateVersion7();
            var netId = Guid.CreateVersion7();
            var rod = new FishingShopItem { Id = rodId, Name = "Unbreakable Rod", Cost = 500, EquipmentSlot = EquipmentSlot.Rod, DisableBreaking = true };
            var reel = new FishingShopItem { Id = reelId, Name = "Unbreakable Reel", Cost = 500, EquipmentSlot = EquipmentSlot.Reel, DisableBreaking = true };
            var box = new FishingShopItem { Id = boxId, Name = "Unbreakable Box", Cost = 500, EquipmentSlot = EquipmentSlot.TackleBox, DisableBreaking = true };
            var net = new FishingShopItem { Id = netId, Name = "Unbreakable Net", Cost = 500, EquipmentSlot = EquipmentSlot.Net, DisableBreaking = true };
            _context.FishingShopItems.AddRange(rod, reel, box, net);

            var bRodId = Guid.CreateVersion7();
            var bReelId = Guid.CreateVersion7();
            var bBoxId = Guid.CreateVersion7();
            var bNetId = Guid.CreateVersion7();
            var bRod = new UserFishingBoost { Id = bRodId, UserId = "user1", ShopItemId = rodId, IsEquipped = true };
            var bReel = new UserFishingBoost { Id = bReelId, UserId = "user1", ShopItemId = reelId, IsEquipped = true };
            var bBox = new UserFishingBoost { Id = bBoxId, UserId = "user1", ShopItemId = boxId, IsEquipped = true };
            var bNet = new UserFishingBoost { Id = bNetId, UserId = "user1", ShopItemId = netId, IsEquipped = true };
            _context.UserFishingBoosts.AddRange(bRod, bReel, bBox, bNet);
            await _context.SaveChangesAsync();

            // Attempt accident losses
            var rodSnap = await _sut.ConsumeItemsOnRodSnap("user1", "user1");
            Assert.Equal(0, rodSnap.TotalGoldLost);
            using (var freshContext = CreateFreshContext())
            {
                Assert.NotNull(await freshContext.UserFishingBoosts.FindAsync(bRodId));
            }

            var reelJam = await _sut.ConsumeItemsOnReelJam("user1", "user1");
            Assert.Equal(0, reelJam.TotalGoldLost);
            using (var freshContext = CreateFreshContext())
            {
                Assert.NotNull(await freshContext.UserFishingBoosts.FindAsync(bReelId));
            }

            var boxLost = await _sut.ConsumeItemsOnTackleBoxLost("user1", "user1");
            Assert.Equal(0, boxLost.TotalGoldLost);
            using (var freshContext = CreateFreshContext())
            {
                Assert.NotNull(await freshContext.UserFishingBoosts.FindAsync(bBoxId));
            }

            var netBreak = await _sut.ConsumeItemsOnNetBreak("user1", "user1");
            Assert.Equal(0, netBreak.TotalGoldLost);
            using (var freshContext = CreateFreshContext())
            {
                Assert.NotNull(await freshContext.UserFishingBoosts.FindAsync(bNetId));
            }
        }

        [Fact]
        public async Task EquipItem_AllowsBrokenItem_CanBeEquippedAndRepaired()
        {
            var shopItemId = Guid.CreateVersion7();
            var boostId = Guid.CreateVersion7();
            var shopItem = new FishingShopItem
            {
                Id = shopItemId,
                Name = "Broken Rod",
                Cost = 100,
                EquipmentSlot = EquipmentSlot.Rod,
                MaxDurability = 50
            };
            _context.FishingShopItems.Add(shopItem);

            var boost = new UserFishingBoost
            {
                Id = boostId,
                UserId = "user1",
                ShopItemId = shopItemId,
                IsEquipped = false,
                CurrentDurability = 0.0
            };
            _context.UserFishingBoosts.Add(boost);
            await _context.SaveChangesAsync();

            await _sut.EquipItem("user1", boostId);

            using var verifyContext = CreateFreshContext();
            var updated = await verifyContext.UserFishingBoosts.FindAsync(boostId);
            Assert.NotNull(updated);
            Assert.True(updated!.IsEquipped);
            Assert.True(updated.IsBroken);
        }

        [Fact]
        public async Task Accidents_SkipBaitAndLureLoss_ForReelTackleBoxAndNet()
        {
            var reelId = Guid.CreateVersion7();
            var baitId = Guid.CreateVersion7();
            var reel = new FishingShopItem { Id = reelId, Name = "Reel", Cost = 100, EquipmentSlot = EquipmentSlot.Reel };
            var bait = new FishingShopItem { Id = baitId, Name = "Bait", Cost = 50, EquipmentSlot = EquipmentSlot.Bait, MaxUses = 5 };
            _context.FishingShopItems.AddRange(reel, bait);

            var bReelId = Guid.CreateVersion7();
            var bBaitId = Guid.CreateVersion7();
            var bReel = new UserFishingBoost { Id = bReelId, UserId = "userAccident", ShopItemId = reelId, IsEquipped = true };
            var bBait = new UserFishingBoost { Id = bBaitId, UserId = "userAccident", ShopItemId = baitId, IsEquipped = true, RemainingUses = 5 };
            _context.UserFishingBoosts.AddRange(bReel, bBait);
            await _context.SaveChangesAsync();

            var reelJamResult = await _sut.ConsumeItemsOnReelJam("userAccident", "userAccident");
            Assert.Equal(100, reelJamResult.TotalGoldLost);

            using var verifyContext = CreateFreshContext();
            var baitAfterJam = await verifyContext.UserFishingBoosts.FindAsync(bBaitId);
            Assert.NotNull(baitAfterJam);
            Assert.Equal(5, baitAfterJam!.RemainingUses);
        }

        [Fact]
        public async Task ConsumeItemDurability_ReturnsBrokenItem_WhenTransitioningFromPositiveToZero()
        {
            var rodShopId = Guid.CreateVersion7();
            var boostId = Guid.CreateVersion7();
            var rod = new FishingShopItem
            {
                Id = rodShopId,
                Name = "Test Rod",
                Cost = 100,
                EquipmentSlot = EquipmentSlot.Rod,
                MaxDurability = 10,
                DurabilityLossPerUse = 10
            };
            _context.FishingShopItems.Add(rod);

            var boost = new UserFishingBoost
            {
                Id = boostId,
                UserId = "userBreak",
                ShopItemId = rodShopId,
                IsEquipped = true,
                CurrentDurability = 5.0
            };
            _context.UserFishingBoosts.Add(boost);
            await _context.SaveChangesAsync();

            var broken = await _sut.ConsumeItemDurability("userBreak", new[] { boostId }, repairCostMultiplier: 0.25);

            Assert.Single(broken);
            Assert.Equal(boostId, broken[0].UserBoostId);
            Assert.Equal(rodShopId, broken[0].ShopItemId);
            Assert.Equal("Test Rod", broken[0].ItemName);
            Assert.Equal(EquipmentSlot.Rod, broken[0].EquipmentSlot);
            Assert.Equal(100, broken[0].ItemCost);
            Assert.False(broken[0].WasReplaced);
        }

        [Fact]
        public async Task ConsumeItemDurability_DoesNotReturnBrokenItem_WhenAlreadyZero()
        {
            var rodShopId = Guid.CreateVersion7();
            var boostId = Guid.CreateVersion7();
            var rod = new FishingShopItem
            {
                Id = rodShopId,
                Name = "Already Broken Rod",
                Cost = 100,
                EquipmentSlot = EquipmentSlot.Rod,
                MaxDurability = 10,
                DurabilityLossPerUse = 10
            };
            _context.FishingShopItems.Add(rod);

            var boost = new UserFishingBoost
            {
                Id = boostId,
                UserId = "userBreak2",
                ShopItemId = rodShopId,
                IsEquipped = true,
                CurrentDurability = 0.0
            };
            _context.UserFishingBoosts.Add(boost);
            await _context.SaveChangesAsync();

            var broken = await _sut.ConsumeItemDurability("userBreak2", new[] { boostId }, repairCostMultiplier: 0.25);

            Assert.Empty(broken);
        }

        [Fact]
        public async Task ConsumeItemDurability_DoesNotReturnBrokenItem_WhenRemainingDurabilityAboveZero()
        {
            var rodShopId = Guid.CreateVersion7();
            var boostId = Guid.CreateVersion7();
            var rod = new FishingShopItem
            {
                Id = rodShopId,
                Name = "Healthy Rod",
                Cost = 100,
                EquipmentSlot = EquipmentSlot.Rod,
                MaxDurability = 100,
                DurabilityLossPerUse = 5
            };
            _context.FishingShopItems.Add(rod);

            var boost = new UserFishingBoost
            {
                Id = boostId,
                UserId = "userBreak3",
                ShopItemId = rodShopId,
                IsEquipped = true,
                CurrentDurability = 80.0
            };
            _context.UserFishingBoosts.Add(boost);
            await _context.SaveChangesAsync();

            var broken = await _sut.ConsumeItemDurability("userBreak3", new[] { boostId }, repairCostMultiplier: 0.25);

            Assert.Empty(broken);
        }
    }
}
