using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Commands.Fishing
{
    public class FishingValueRulesTests
    {
        [Fact]
        public void NormalizeAndValidate_ShopItem_Throws_WhenSpecificFishBoostHasNoTargetFish()
        {
            var item = new FishingShopItem
            {
                BoostType = FishingBoostType.SpecificFishBoost,
                TargetFishTypeId = null
            };

            var ex = Assert.Throws<ArgumentException>(() => FishingValueRules.NormalizeAndValidate(item));
            Assert.Contains("Target fish is required", ex.Message);
        }

        [Fact]
        public void NormalizeAndValidate_ShopItem_Throws_WhenSecondarySpecificFishBoostHasNoTargetFish()
        {
            var item = new FishingShopItem
            {
                BoostType = FishingBoostType.GeneralRarityBoost,
                BoostType2 = FishingBoostType.SpecificFishBoost,
                TargetFishTypeId = null
            };

            var ex = Assert.Throws<ArgumentException>(() => FishingValueRules.NormalizeAndValidate(item));
            Assert.Contains("Target fish is required", ex.Message);
        }

        [Fact]
        public void NormalizeAndValidate_ShopItem_Throws_WhenSpecificCategoryBoostHasBlankCategory()
        {
            var item = new FishingShopItem
            {
                BoostType = FishingBoostType.SpecificCategoryBoost,
                TargetCategory = "   "
            };

            var ex = Assert.Throws<ArgumentException>(() => FishingValueRules.NormalizeAndValidate(item));
            Assert.Contains("Target category is required", ex.Message);
        }

        [Fact]
        public void NormalizeAndValidate_ShopItem_Throws_WhenTertiarySpecificCategoryBoostHasNullCategory()
        {
            var item = new FishingShopItem
            {
                BoostType = FishingBoostType.GeneralRarityBoost,
                BoostType3 = FishingBoostType.SpecificCategoryBoost,
                TargetCategory = null
            };

            var ex = Assert.Throws<ArgumentException>(() => FishingValueRules.NormalizeAndValidate(item));
            Assert.Contains("Target category is required", ex.Message);
        }

        [Fact]
        public void NormalizeAndValidate_ShopItem_Succeeds_WhenTargetsAreProvided()
        {
            var item = new FishingShopItem
            {
                BoostType = FishingBoostType.SpecificFishBoost,
                BoostAmount = 0.1,
                TargetFishTypeId = 5,
                BoostType2 = FishingBoostType.SpecificCategoryBoost,
                BoostAmount2 = 0.2,
                TargetCategory = "River"
            };

            FishingValueRules.NormalizeAndValidate(item);

            Assert.Equal(0.1, item.BoostAmount);
            Assert.Equal(0.2, item.BoostAmount2);
            Assert.Equal(5, item.TargetFishTypeId);
            Assert.Equal("River", item.TargetCategory);
        }

        [Theory]
        [InlineData(-1.5, -0.8)]
        [InlineData(-0.8, -0.8)]
        [InlineData(0.1234, 0.12)]
        [InlineData(5.0, 5.0)]
        [InlineData(10.0, 5.0)]
        public void ClampBoostAmount_ClampsAndRoundsCorrectly(double input, double expected)
        {
            var result = FishingValueRules.ClampBoostAmount(input);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void NormalizeOptionalBoost_ReturnsNulls_WhenBoostTypeIsNull()
        {
            var (type, amount) = FishingValueRules.NormalizeOptionalBoost(null, 0.25);
            Assert.Null(type);
            Assert.Null(amount);
        }

        [Fact]
        public void NormalizeOptionalBoost_DefaultsAmountToZero_WhenBoostTypeIsNotNullAndAmountIsNull()
        {
            var (type, amount) = FishingValueRules.NormalizeOptionalBoost(FishingBoostType.GeneralRarityBoost, null);
            Assert.Equal(FishingBoostType.GeneralRarityBoost, type);
            Assert.Equal(0.0, amount);
        }

        [Fact]
        public void NormalizeOptionalBoost_ClampsAmount_WhenProvided()
        {
            var (type, amount) = FishingValueRules.NormalizeOptionalBoost(FishingBoostType.WeightBoost, 6.5);
            Assert.Equal(FishingBoostType.WeightBoost, type);
            Assert.Equal(5.0, amount);
        }

        [Fact]
        public void ResolveOptionalBoost_KeepCurrent_PreservesExistingValues()
        {
            var (type, amount) = FishingValueRules.ResolveOptionalBoost(
                "KeepCurrent",
                null,
                FishingBoostType.SpecificFishBoost,
                0.35);

            Assert.Equal(FishingBoostType.SpecificFishBoost, type);
            Assert.Equal(0.35, amount);
        }

        [Fact]
        public void ResolveOptionalBoost_None_ClearsBoost()
        {
            var (type, amount) = FishingValueRules.ResolveOptionalBoost(
                "None",
                0.5, // amount should be ignored/cleared because type is None
                FishingBoostType.SpecificFishBoost,
                0.35);

            Assert.Null(type);
            Assert.Null(amount);
        }

        [Fact]
        public void ResolveOptionalBoost_NewTypeAndAmount_UpdatesBoth()
        {
            var (type, amount) = FishingValueRules.ResolveOptionalBoost(
                "WeightBoost",
                0.45,
                null,
                null);

            Assert.Equal(FishingBoostType.WeightBoost, type);
            Assert.Equal(0.45, amount);
        }

        [Fact]
        public void ClearUnusedShopItemTargets_ClearsTargets_WhenBoostsNotPresent()
        {
            var item = new FishingShopItem
            {
                BoostType = FishingBoostType.GeneralRarityBoost,
                TargetFishTypeId = 10,
                TargetCategory = "River"
            };

            FishingValueRules.ClearUnusedShopItemTargets(item);

            Assert.Null(item.TargetFishTypeId);
            Assert.Null(item.TargetCategory);
        }

        [Fact]
        public void ClearUnusedShopItemTargets_PreservesTargets_WhenBoostsPresent()
        {
            var item = new FishingShopItem
            {
                BoostType = FishingBoostType.SpecificFishBoost,
                TargetFishTypeId = 10,
                BoostType2 = FishingBoostType.SpecificCategoryBoost,
                TargetCategory = "River"
            };

            FishingValueRules.ClearUnusedShopItemTargets(item);

            Assert.Equal(10, item.TargetFishTypeId);
            Assert.Equal("River", item.TargetCategory);
        }

        [Fact]
        public void ValidateShopItemTargets_ReturnsFalse_WhenSpecificFishBoostMissingTarget()
        {
            var item = new FishingShopItem
            {
                BoostType = FishingBoostType.SpecificFishBoost,
                TargetFishTypeId = null
            };

            var isValid = FishingValueRules.ValidateShopItemTargets(item, out var error);

            Assert.False(isValid);
            Assert.Contains("Target fish is required", error);
        }

        [Fact]
        public void ValidateShopItemTargets_ReturnsFalse_WhenSpecificCategoryBoostMissingTarget()
        {
            var item = new FishingShopItem
            {
                BoostType = FishingBoostType.SpecificCategoryBoost,
                TargetCategory = "   "
            };

            var isValid = FishingValueRules.ValidateShopItemTargets(item, out var error);

            Assert.False(isValid);
            Assert.Contains("Target category is required", error);
        }

        [Fact]
        public void NormalizeShopItem_NormalizesAmountsAndTargets()
        {
            var item = new FishingShopItem
            {
                BoostType = FishingBoostType.GeneralRarityBoost,
                BoostAmount = 6.0,
                BoostType2 = null,
                BoostAmount2 = 0.5,
                BoostType3 = FishingBoostType.WeightBoost,
                BoostAmount3 = null,
                TargetCategory = "  Ocean  "
            };

            FishingValueRules.NormalizeShopItem(item);

            Assert.Equal(5.0, item.BoostAmount);
            Assert.Null(item.BoostType2);
            Assert.Null(item.BoostAmount2);
            Assert.Equal(FishingBoostType.WeightBoost, item.BoostType3);
            Assert.Equal(0.0, item.BoostAmount3);
            Assert.Equal("Ocean", item.TargetCategory);
        }
    }
}

