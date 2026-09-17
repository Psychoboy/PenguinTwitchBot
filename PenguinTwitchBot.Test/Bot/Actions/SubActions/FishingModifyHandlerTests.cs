using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
using PenguinTwitchBot.Bot.Commands.Fishing;
using System.Collections.Concurrent;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class FishingModifyHandlerTests
    {
        private readonly IFishingService _fishingService = Substitute.For<IFishingService>();
        private readonly IFishingShopService _fishingShopService = Substitute.For<IFishingShopService>();
        private readonly ILogger<FishingModifyHandler> _logger = Substitute.For<ILogger<FishingModifyHandler>>();
        private readonly FishingModifyHandler _handler;

        public FishingModifyHandlerTests()
        {
            _handler = new FishingModifyHandler(_fishingService, _fishingShopService, _logger);
        }

        [Fact]
        public async Task ExecuteAsync_Throws_WhenInvalidSubActionType()
        {
            var variables = new ConcurrentDictionary<string, string>();
            await Assert.ThrowsAsync<SubActionHandlerException>(() =>
                _handler.ExecuteAsync(new FishingType(), variables));
        }

        [Fact]
        public async Task ExecuteAsync_Fish_Throws_WhenTargetFishEmpty()
        {
            var subAction = new FishingModifyType
            {
                TargetType = FishingModifyTargetType.Fish,
                TargetFish = ""
            };
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAsync<SubActionUserFacingException>(() =>
                _handler.ExecuteAsync(subAction, variables));
        }

        [Fact]
        public async Task ExecuteAsync_Fish_Throws_WhenFishNotFound()
        {
            _fishingService.GetFishTypeById(99).Returns((FishType?)null);
            _fishingService.GetAllFishTypes().Returns(new List<FishType>());

            var subAction = new FishingModifyType
            {
                TargetType = FishingModifyTargetType.Fish,
                TargetFish = "99"
            };
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAsync<SubActionUserFacingException>(() =>
                _handler.ExecuteAsync(subAction, variables));
        }

        [Fact]
        public async Task ExecuteAsync_Fish_ModifiesNameAndGold()
        {
            var fish = new FishType
            {
                Id = 1,
                Name = "Old Salmon",
                BaseGold = 50,
                Rarity = FishRarity.Rare,
                Enabled = true
            };
            _fishingService.GetFishTypeById(1).Returns(fish);

            var subAction = new FishingModifyType
            {
                TargetType = FishingModifyTargetType.Fish,
                TargetFish = "1",
                NewName = "Grand Salmon",
                NewGold = "80",
                RarityMode = FishingModifyRarityMode.KeepCurrent,
                EnabledState = ItemEnabledStateAction.KeepCurrent
            };
            var variables = new ConcurrentDictionary<string, string>();

            await _handler.ExecuteAsync(subAction, variables);

            Assert.Equal("Grand Salmon", fish.Name);
            Assert.Equal(80, fish.BaseGold);
            Assert.Equal(FishRarity.Rare, fish.Rarity);
            Assert.True(fish.Enabled);

            await _fishingService.Received(1).UpdateFishType(fish);
            Assert.Equal("Grand Salmon", variables["modified_fish_name"]);
            Assert.Equal("80", variables["modified_fish_gold"]);
            Assert.Equal("Rare", variables["modified_fish_rarity"]);
        }

        [Fact]
        public async Task ExecuteAsync_Fish_AutoCalculatesRarityFromGold_EpicToLegendary()
        {
            // Settings: Uncommon=35, Rare=60, Epic=110, Legendary=201, Mythical=300
            var settings = new FishingSettings
            {
                RarityUncommonThreshold = 35,
                RarityRareThreshold = 60,
                RarityEpicThreshold = 110,
                RarityLegendaryThreshold = 201,
                RarityMythicalThreshold = 300
            };
            _fishingService.GetSettings().Returns(settings);

            var fish = new FishType
            {
                Id = 5,
                Name = "Trout",
                BaseGold = 150, // Epic
                Rarity = FishRarity.Epic,
                Enabled = true
            };
            _fishingService.GetFishTypeById(5).Returns(fish);

            // Increase gold to 250 (which exceeds Legendary threshold of 201)
            var subAction = new FishingModifyType
            {
                TargetType = FishingModifyTargetType.Fish,
                TargetFish = "5",
                NewGold = "250",
                RarityMode = FishingModifyRarityMode.AutoFromGold
            };
            var variables = new ConcurrentDictionary<string, string>();

            await _handler.ExecuteAsync(subAction, variables);

            Assert.Equal(250, fish.BaseGold);
            Assert.Equal(FishRarity.Legendary, fish.Rarity);
            await _fishingService.Received(1).UpdateFishType(fish);
            Assert.Equal("Legendary", variables["modified_fish_rarity"]);
        }

        [Fact]
        public async Task ExecuteAsync_Fish_ManualRarity_SetsExplicitRarity()
        {
            var fish = new FishType
            {
                Id = 2,
                Name = "Bass",
                BaseGold = 20,
                Rarity = FishRarity.Common,
                Enabled = true
            };
            _fishingService.GetFishTypeById(2).Returns(fish);

            var subAction = new FishingModifyType
            {
                TargetType = FishingModifyTargetType.Fish,
                TargetFish = "2",
                RarityMode = FishingModifyRarityMode.Manual,
                ManualRarity = FishRarity.Mythical
            };
            var variables = new ConcurrentDictionary<string, string>();

            await _handler.ExecuteAsync(subAction, variables);

            Assert.Equal(FishRarity.Mythical, fish.Rarity);
            await _fishingService.Received(1).UpdateFishType(fish);
            Assert.Equal("Mythical", variables["modified_fish_rarity"]);
        }

        [Fact]
        public async Task ExecuteAsync_Fish_EnablesDisablesTogglesState()
        {
            var fish = new FishType
            {
                Id = 3,
                Name = "Cod",
                Enabled = true
            };
            _fishingService.GetFishTypeById(3).Returns(fish);

            // Test Disable
            var disableAction = new FishingModifyType
            {
                TargetType = FishingModifyTargetType.Fish,
                TargetFish = "3",
                EnabledState = ItemEnabledStateAction.Disable
            };
            await _handler.ExecuteAsync(disableAction, new ConcurrentDictionary<string, string>());
            Assert.False(fish.Enabled);

            // Test Toggle
            var toggleAction = new FishingModifyType
            {
                TargetType = FishingModifyTargetType.Fish,
                TargetFish = "3",
                EnabledState = ItemEnabledStateAction.Toggle
            };
            await _handler.ExecuteAsync(toggleAction, new ConcurrentDictionary<string, string>());
            Assert.True(fish.Enabled);

            // Test Enable
            var enableAction = new FishingModifyType
            {
                TargetType = FishingModifyTargetType.Fish,
                TargetFish = "3",
                EnabledState = ItemEnabledStateAction.Enable
            };
            await _handler.ExecuteAsync(enableAction, new ConcurrentDictionary<string, string>());
            Assert.True(fish.Enabled);
        }

        [Fact]
        public async Task ExecuteAsync_Fish_ResolvesTargetByNameOrVariable()
        {
            var fish = new FishType
            {
                Id = 7,
                Name = "Rainbow Trout",
                BaseGold = 100,
                Rarity = FishRarity.Rare,
                Enabled = true
            };
            _fishingService.GetFishTypeById(Arg.Any<int>()).Returns((FishType?)null);
            _fishingService.GetAllFishTypes().Returns(new List<FishType> { fish });

            var subAction = new FishingModifyType
            {
                TargetType = FishingModifyTargetType.Fish,
                TargetFish = "%target_fish%",
                NewName = "%new_name%",
                NewGold = "%new_gold%"
            };
            var variables = new ConcurrentDictionary<string, string>
            {
                ["target_fish"] = "Rainbow Trout",
                ["new_name"] = "Super Rainbow Trout",
                ["new_gold"] = "199"
            };

            await _handler.ExecuteAsync(subAction, variables);

            Assert.Equal("Super Rainbow Trout", fish.Name);
            Assert.Equal(199, fish.BaseGold);
            await _fishingService.Received(1).UpdateFishType(fish);
        }

        [Fact]
        public async Task ExecuteAsync_ShopItem_Throws_WhenTargetShopItemEmpty()
        {
            var subAction = new FishingModifyType
            {
                TargetType = FishingModifyTargetType.ShopItem,
                TargetShopItem = ""
            };
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAsync<SubActionUserFacingException>(() =>
                _handler.ExecuteAsync(subAction, variables));
        }

        [Fact]
        public async Task ExecuteAsync_ShopItem_Throws_WhenShopItemNotFound()
        {
            _fishingShopService.GetShopItemById(88).Returns((FishingShopItem?)null);
            _fishingShopService.GetAllShopItems().Returns(new List<FishingShopItem>());

            var subAction = new FishingModifyType
            {
                TargetType = FishingModifyTargetType.ShopItem,
                TargetShopItem = "88"
            };
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAsync<SubActionUserFacingException>(() =>
                _handler.ExecuteAsync(subAction, variables));
        }

        [Fact]
        public async Task ExecuteAsync_ShopItem_ModifiesNameCostDescriptionAndEnabled()
        {
            var item = new FishingShopItem
            {
                Id = 12,
                Name = "Basic Hook",
                Cost = 100,
                Description = "Standard hook",
                Enabled = true
            };
            _fishingShopService.GetShopItemById(12).Returns(item);

            var subAction = new FishingModifyType
            {
                TargetType = FishingModifyTargetType.ShopItem,
                TargetShopItem = "12",
                NewName = "Golden Hook",
                NewCost = "350",
                NewDescription = "Hook plated with gold",
                EnabledState = ItemEnabledStateAction.Disable
            };
            var variables = new ConcurrentDictionary<string, string>();

            await _handler.ExecuteAsync(subAction, variables);

            Assert.Equal("Golden Hook", item.Name);
            Assert.Equal(350, item.Cost);
            Assert.Equal("Hook plated with gold", item.Description);
            Assert.False(item.Enabled);

            await _fishingShopService.Received(1).UpdateShopItem(item);
            Assert.Equal("ShopItem", variables["modified_target_type"]);
            Assert.Equal("12", variables["modified_shop_item_id"]);
            Assert.Equal("Golden Hook", variables["modified_shop_item_name"]);
            Assert.Equal("350", variables["modified_shop_item_cost"]);
            Assert.Equal("false", variables["modified_shop_item_enabled"]);
        }

        [Fact]
        public async Task ExecuteAsync_ShopItem_ResolvesTargetByNameOrVariable()
        {
            var item = new FishingShopItem
            {
                Id = 15,
                Name = "Pro Rod",
                Cost = 500,
                Enabled = true
            };
            _fishingShopService.GetShopItemById(Arg.Any<int>()).Returns((FishingShopItem?)null);
            _fishingShopService.GetAllShopItems().Returns(new List<FishingShopItem> { item });

            var subAction = new FishingModifyType
            {
                TargetType = FishingModifyTargetType.ShopItem,
                TargetShopItem = "%shop_item%",
                NewCost = "%discounted_cost%",
                EnabledState = ItemEnabledStateAction.Toggle
            };
            var variables = new ConcurrentDictionary<string, string>
            {
                ["shop_item"] = "Pro Rod",
                ["discounted_cost"] = "400"
            };

            await _handler.ExecuteAsync(subAction, variables);

            Assert.Equal(400, item.Cost);
            Assert.False(item.Enabled);
            await _fishingShopService.Received(1).UpdateShopItem(item);
        }

        [Fact]
        public async Task ExecuteAsync_ShopItem_ModifiesBoostTypeAndAmount()
        {
            var item = new FishingShopItem
            {
                Id = 18,
                Name = "Lucky Charm",
                Cost = 200,
                BoostType = FishingBoostType.GeneralRarityBoost,
                BoostAmount = 0.05,
                Enabled = true
            };
            _fishingShopService.GetShopItemById(18).Returns(item);

            var subAction = new FishingModifyType
            {
                TargetType = FishingModifyTargetType.ShopItem,
                TargetShopItem = "18",
                NewBoostType = FishingBoostType.StarBoost,
                NewBoostAmount = "0.15"
            };
            var variables = new ConcurrentDictionary<string, string>();

            await _handler.ExecuteAsync(subAction, variables);

            Assert.Equal(FishingBoostType.StarBoost, item.BoostType);
            Assert.Equal(0.15, item.BoostAmount);
            await _fishingShopService.Received(1).UpdateShopItem(item);
            Assert.Equal("StarBoost", variables["modified_shop_item_boost_type"]);
            Assert.Equal("0.15", variables["modified_shop_item_boost_amount"]);
        }

        [Fact]
        public async Task ExecuteAsync_ShopItem_ModifiesSpecificFishAndCategoryBoosts()
        {
            var fish = new FishType { Id = 8, Name = "Salmon" };
            _fishingService.GetAllFishTypes().Returns(new List<FishType> { fish });

            var item = new FishingShopItem
            {
                Id = 25,
                Name = "Salmon Bait",
                BoostType = FishingBoostType.SpecificFishBoost,
                TargetFishTypeId = null,
                TargetCategory = null
            };
            _fishingShopService.GetShopItemById(25).Returns(item);

            var subAction = new FishingModifyType
            {
                TargetType = FishingModifyTargetType.ShopItem,
                TargetShopItem = "25",
                NewTargetFish = "Salmon",
                NewTargetCategory = "Freshwater"
            };
            var variables = new ConcurrentDictionary<string, string>();

            await _handler.ExecuteAsync(subAction, variables);

            Assert.Equal(8, item.TargetFishTypeId);
            Assert.Equal("Freshwater", item.TargetCategory);
            await _fishingShopService.Received(1).UpdateShopItem(item);
        }

        [Fact]
        public async Task ExecuteAsync_ShopItem_ModifiesEquipmentSlotMaxUsesAndAdminOnly()
        {
            var item = new FishingShopItem
            {
                Id = 30,
                Name = "Mythic Reel",
                EquipmentSlot = null,
                MaxUses = null,
                IsAdminOnly = false
            };
            _fishingShopService.GetShopItemById(30).Returns(item);

            var subAction = new FishingModifyType
            {
                TargetType = FishingModifyTargetType.ShopItem,
                TargetShopItem = "30",
                NewEquipmentSlot = nameof(EquipmentSlot.Reel),
                NewMaxUses = "25",
                AdminOnlyState = ItemEnabledStateAction.Enable
            };
            var variables = new ConcurrentDictionary<string, string>();

            await _handler.ExecuteAsync(subAction, variables);

            Assert.Equal(EquipmentSlot.Reel, item.EquipmentSlot);
            Assert.Equal(25, item.MaxUses);
            Assert.True(item.IsAdminOnly);
            await _fishingShopService.Received(1).UpdateShopItem(item);
            Assert.Equal("Reel", variables["modified_shop_item_equipment_slot"]);
            Assert.Equal("25", variables["modified_shop_item_max_uses"]);
            Assert.Equal("true", variables["modified_shop_item_admin_only"]);
        }

        [Fact]
        public async Task ExecuteAsync_ShopItem_ClearsEquipmentSlotAndSetsUnlimitedUses()
        {
            var item = new FishingShopItem
            {
                Id = 31,
                Name = "Special Lure",
                EquipmentSlot = EquipmentSlot.Lure,
                MaxUses = 5,
                IsAdminOnly = true
            };
            _fishingShopService.GetShopItemById(31).Returns(item);

            var subAction = new FishingModifyType
            {
                TargetType = FishingModifyTargetType.ShopItem,
                TargetShopItem = "31",
                NewEquipmentSlot = "None",
                NewMaxUses = "unlimited",
                AdminOnlyState = ItemEnabledStateAction.Disable
            };
            var variables = new ConcurrentDictionary<string, string>();

            await _handler.ExecuteAsync(subAction, variables);

            Assert.Null(item.EquipmentSlot);
            Assert.Null(item.MaxUses);
            Assert.False(item.IsAdminOnly);
            await _fishingShopService.Received(1).UpdateShopItem(item);
            Assert.Equal("None", variables["modified_shop_item_equipment_slot"]);
            Assert.Equal("Unlimited", variables["modified_shop_item_max_uses"]);
            Assert.Equal("false", variables["modified_shop_item_admin_only"]);
        }

        [Fact]
        public async Task ExecuteAsync_ShopItem_UpdatesSecondaryAndTertiaryBoosts()
        {
            var item = new FishingShopItem
            {
                Id = 32,
                Name = "Tackle Box",
                BoostType = FishingBoostType.GeneralRarityBoost,
                BoostAmount = 0.05,
                BoostType2 = null,
                BoostAmount2 = null,
                BoostType3 = null,
                BoostAmount3 = null
            };
            _fishingShopService.GetShopItemById(32).Returns(item);

            var subAction = new FishingModifyType
            {
                TargetType = FishingModifyTargetType.ShopItem,
                TargetShopItem = "32",
                NewBoostType2 = nameof(FishingBoostType.WeightBoost),
                NewBoostAmount2 = "0.15",
                NewBoostType3 = nameof(FishingBoostType.StarBoost),
                NewBoostAmount3 = "0.08"
            };
            var variables = new ConcurrentDictionary<string, string>();

            await _handler.ExecuteAsync(subAction, variables);

            Assert.Equal(FishingBoostType.WeightBoost, item.BoostType2);
            Assert.Equal(0.15, item.BoostAmount2);
            Assert.Equal(FishingBoostType.StarBoost, item.BoostType3);
            Assert.Equal(0.08, item.BoostAmount3);
            await _fishingShopService.Received(1).UpdateShopItem(item);
            Assert.Equal("WeightBoost", variables["modified_shop_item_boost_type2"]);
            Assert.Equal("0.15", variables["modified_shop_item_boost_amount2"]);
            Assert.Equal("StarBoost", variables["modified_shop_item_boost_type3"]);
            Assert.Equal("0.08", variables["modified_shop_item_boost_amount3"]);
        }

        [Fact]
        public async Task ExecuteAsync_ShopItem_RemovesSecondaryAndTertiaryBoosts()
        {
            var item = new FishingShopItem
            {
                Id = 33,
                Name = "Grand Tackle Box",
                BoostType = FishingBoostType.GeneralRarityBoost,
                BoostAmount = 0.10,
                BoostType2 = FishingBoostType.WeightBoost,
                BoostAmount2 = 0.15,
                BoostType3 = FishingBoostType.StarBoost,
                BoostAmount3 = 0.05
            };
            _fishingShopService.GetShopItemById(33).Returns(item);

            var subAction = new FishingModifyType
            {
                TargetType = FishingModifyTargetType.ShopItem,
                TargetShopItem = "33",
                NewBoostType2 = "None",
                NewBoostType3 = "None"
            };
            var variables = new ConcurrentDictionary<string, string>();

            await _handler.ExecuteAsync(subAction, variables);

            Assert.Null(item.BoostType2);
            Assert.Null(item.BoostAmount2);
            Assert.Null(item.BoostType3);
            Assert.Null(item.BoostAmount3);
            await _fishingShopService.Received(1).UpdateShopItem(item);
            Assert.Equal("None", variables["modified_shop_item_boost_type2"]);
            Assert.Equal("0", variables["modified_shop_item_boost_amount2"]);
            Assert.Equal("None", variables["modified_shop_item_boost_type3"]);
            Assert.Equal("0", variables["modified_shop_item_boost_amount3"]);
        }
    }
}
