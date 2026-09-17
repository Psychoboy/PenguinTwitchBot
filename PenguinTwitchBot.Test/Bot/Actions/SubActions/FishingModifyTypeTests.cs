using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class FishingModifyTypeTests
    {
        [Fact]
        public void GetUIFields_FishMode_ReturnsExpectedFields()
        {
            var subAction = new FishingModifyType
            {
                TargetType = FishingModifyTargetType.Fish,
                RarityMode = FishingModifyRarityMode.AutoFromGold
            };

            var fields = subAction.GetUIFields();

            Assert.Contains(fields, f => f.PropertyName == nameof(FishingModifyType.TargetType));
            Assert.Contains(fields, f => f.PropertyName == nameof(FishingModifyType.TargetFish));
            Assert.Contains(fields, f => f.PropertyName == nameof(FishingModifyType.NewName));
            Assert.Contains(fields, f => f.PropertyName == nameof(FishingModifyType.NewGold));
            Assert.Contains(fields, f => f.PropertyName == nameof(FishingModifyType.RarityMode));
            Assert.Contains(fields, f => f.PropertyName == nameof(FishingModifyType.EnabledState));
            Assert.Contains(fields, f => f.PropertyName == nameof(FishingModifyType.Enabled));
            Assert.DoesNotContain(fields, f => f.PropertyName == nameof(FishingModifyType.ManualRarity));
            Assert.DoesNotContain(fields, f => f.PropertyName == nameof(FishingModifyType.TargetShopItem));
        }

        [Fact]
        public void GetUIFields_FishMode_ManualRarity_IncludesManualRarityField()
        {
            var subAction = new FishingModifyType
            {
                TargetType = FishingModifyTargetType.Fish,
                RarityMode = FishingModifyRarityMode.Manual
            };

            var fields = subAction.GetUIFields();

            Assert.Contains(fields, f => f.PropertyName == nameof(FishingModifyType.ManualRarity));
        }

        [Fact]
        public void GetUIFields_ShopItemMode_ReturnsExpectedFields()
        {
            var subAction = new FishingModifyType
            {
                TargetType = FishingModifyTargetType.ShopItem
            };

            var fields = subAction.GetUIFields();

            Assert.Contains(fields, f => f.PropertyName == nameof(FishingModifyType.TargetType));
            Assert.Contains(fields, f => f.PropertyName == nameof(FishingModifyType.TargetShopItem));
            Assert.Contains(fields, f => f.PropertyName == nameof(FishingModifyType.NewName));
            Assert.Contains(fields, f => f.PropertyName == nameof(FishingModifyType.NewCost));
            Assert.Contains(fields, f => f.PropertyName == nameof(FishingModifyType.NewDescription));
            Assert.Contains(fields, f => f.PropertyName == nameof(FishingModifyType.NewBoostType));
            Assert.Contains(fields, f => f.PropertyName == nameof(FishingModifyType.NewBoostAmount));
            Assert.Contains(fields, f => f.PropertyName == nameof(FishingModifyType.NewEquipmentSlot));
            Assert.Contains(fields, f => f.PropertyName == nameof(FishingModifyType.NewMaxUses));
            Assert.Contains(fields, f => f.PropertyName == nameof(FishingModifyType.AdminOnlyState));
            Assert.Contains(fields, f => f.PropertyName == nameof(FishingModifyType.EnabledState));
            Assert.Contains(fields, f => f.PropertyName == nameof(FishingModifyType.Enabled));
            Assert.DoesNotContain(fields, f => f.PropertyName == nameof(FishingModifyType.TargetFish));
            Assert.DoesNotContain(fields, f => f.PropertyName == nameof(FishingModifyType.RarityMode));
        }

        [Fact]
        public void GetUIFields_ShopItemMode_SpecificFishBoost_IncludesNewTargetFish()
        {
            var subAction = new FishingModifyType
            {
                TargetType = FishingModifyTargetType.ShopItem,
                NewBoostType = FishingBoostType.SpecificFishBoost
            };

            var fields = subAction.GetUIFields();

            Assert.Contains(fields, f => f.PropertyName == nameof(FishingModifyType.NewTargetFish));
            Assert.DoesNotContain(fields, f => f.PropertyName == nameof(FishingModifyType.NewTargetCategory));
        }

        [Fact]
        public void GetUIFields_ShopItemMode_SpecificCategoryBoost_IncludesNewTargetCategory()
        {
            var subAction = new FishingModifyType
            {
                TargetType = FishingModifyTargetType.ShopItem,
                NewBoostType = FishingBoostType.SpecificCategoryBoost
            };

            var fields = subAction.GetUIFields();

            Assert.Contains(fields, f => f.PropertyName == nameof(FishingModifyType.NewTargetCategory));
            Assert.DoesNotContain(fields, f => f.PropertyName == nameof(FishingModifyType.NewTargetFish));
        }

        [Fact]
        public void GetValues_And_SetValues_Roundtrip_PreservesAllProperties()
        {
            var original = new FishingModifyType
            {
                TargetType = FishingModifyTargetType.Fish,
                TargetFish = "42",
                TargetShopItem = "10",
                NewName = "Super Salmon",
                NewGold = "250",
                NewCost = "100",
                NewDescription = "Updated description",
                RarityMode = FishingModifyRarityMode.AutoFromGold,
                ManualRarity = FishRarity.Epic,
                EnabledState = ItemEnabledStateAction.Disable,
                NewBoostType = FishingBoostType.SpecificFishBoost,
                NewBoostAmount = "0.25",
                NewTargetFish = "Salmon",
                NewTargetCategory = "River",
                NewEquipmentSlot = nameof(EquipmentSlot.Rod),
                NewMaxUses = "15",
                AdminOnlyState = ItemEnabledStateAction.Enable,
                Enabled = false
            };

            var values = original.GetValues();

            var restored = new FishingModifyType();
            restored.SetValues(values);

            Assert.Equal(original.TargetType, restored.TargetType);
            Assert.Equal(original.TargetFish, restored.TargetFish);
            Assert.Equal(original.TargetShopItem, restored.TargetShopItem);
            Assert.Equal(original.NewName, restored.NewName);
            Assert.Equal(original.NewGold, restored.NewGold);
            Assert.Equal(original.NewCost, restored.NewCost);
            Assert.Equal(original.NewDescription, restored.NewDescription);
            Assert.Equal(original.RarityMode, restored.RarityMode);
            Assert.Equal(original.ManualRarity, restored.ManualRarity);
            Assert.Equal(original.EnabledState, restored.EnabledState);
            Assert.Equal(original.NewBoostType, restored.NewBoostType);
            Assert.Equal(original.NewBoostAmount, restored.NewBoostAmount);
            Assert.Equal(original.NewTargetFish, restored.NewTargetFish);
            Assert.Equal(original.NewTargetCategory, restored.NewTargetCategory);
            Assert.Equal(original.NewEquipmentSlot, restored.NewEquipmentSlot);
            Assert.Equal(original.NewMaxUses, restored.NewMaxUses);
            Assert.Equal(original.AdminOnlyState, restored.AdminOnlyState);
            Assert.Equal(original.Enabled, restored.Enabled);
        }

        [Fact]
        public void Validate_Fish_RequiresTargetFish()
        {
            var subAction = new FishingModifyType { TargetType = FishingModifyTargetType.Fish };

            var error = subAction.Validate(new Dictionary<string, object?>
            {
                [nameof(FishingModifyType.TargetType)] = nameof(FishingModifyTargetType.Fish),
                [nameof(FishingModifyType.TargetFish)] = ""
            });

            Assert.Equal("Fish to Modify is required", error);
        }

        [Fact]
        public void Validate_Fish_RejectsInvalidGold()
        {
            var subAction = new FishingModifyType { TargetType = FishingModifyTargetType.Fish };

            var error = subAction.Validate(new Dictionary<string, object?>
            {
                [nameof(FishingModifyType.TargetType)] = nameof(FishingModifyTargetType.Fish),
                [nameof(FishingModifyType.TargetFish)] = "1",
                [nameof(FishingModifyType.NewGold)] = "-5"
            });

            Assert.Equal("New Base Gold must be a valid non-negative number or variable", error);
        }

        [Fact]
        public void Validate_Fish_AcceptsVariableGold()
        {
            var subAction = new FishingModifyType { TargetType = FishingModifyTargetType.Fish };

            var error = subAction.Validate(new Dictionary<string, object?>
            {
                [nameof(FishingModifyType.TargetType)] = nameof(FishingModifyTargetType.Fish),
                [nameof(FishingModifyType.TargetFish)] = "1",
                [nameof(FishingModifyType.NewGold)] = "%gold_value%"
            });

            Assert.Null(error);
        }

        [Fact]
        public void Validate_ShopItem_RequiresTargetShopItem()
        {
            var subAction = new FishingModifyType { TargetType = FishingModifyTargetType.ShopItem };

            var error = subAction.Validate(new Dictionary<string, object?>
            {
                [nameof(FishingModifyType.TargetType)] = nameof(FishingModifyTargetType.ShopItem),
                [nameof(FishingModifyType.TargetShopItem)] = ""
            });

            Assert.Equal("Shop Item to Modify is required", error);
        }

        [Fact]
        public void Validate_ShopItem_RejectsInvalidCost()
        {
            var subAction = new FishingModifyType { TargetType = FishingModifyTargetType.ShopItem };

            var error = subAction.Validate(new Dictionary<string, object?>
            {
                [nameof(FishingModifyType.TargetType)] = nameof(FishingModifyTargetType.ShopItem),
                [nameof(FishingModifyType.TargetShopItem)] = "1",
                [nameof(FishingModifyType.NewCost)] = "invalid_cost"
            });

            Assert.Equal("New Cost must be a valid non-negative number or variable", error);
        }

        [Fact]
        public void Validate_ShopItem_RejectsInvalidBoostAmount()
        {
            var subAction = new FishingModifyType { TargetType = FishingModifyTargetType.ShopItem };

            var error = subAction.Validate(new Dictionary<string, object?>
            {
                [nameof(FishingModifyType.TargetType)] = nameof(FishingModifyTargetType.ShopItem),
                [nameof(FishingModifyType.TargetShopItem)] = "1",
                [nameof(FishingModifyType.NewBoostAmount)] = "not_a_number"
            });

            Assert.Equal("New Boost Amount must be a valid number or variable", error);
        }
    }
}
