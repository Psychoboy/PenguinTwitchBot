using Bunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Components.Fishing;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
using Xunit;

namespace PenguinTwitchBot.Test.Components
{
    public class ItemPurchasePreviewTests
    {
        private BunitContext CreateTestContext()
        {
            var ctx = new BunitContext();
            ctx.JSInterop.Mode = JSRuntimeMode.Loose;
            ctx.Services.AddMudServices();
            ctx.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

            var fishingService = Substitute.For<IFishingService>();
            var analyticsService = Substitute.For<IFishingAnalyticsService>();

            ctx.Services.AddSingleton(fishingService);
            ctx.Services.AddSingleton(analyticsService);

            fishingService.GetAllFishTypes().Returns(new List<FishType>
            {
                new() { Id = 1, Name = "Common Bass", Rarity = FishRarity.Common, Enabled = true },
                new() { Id = 2, Name = "Uncommon Trout", Rarity = FishRarity.Uncommon, Enabled = true }
            });
            fishingService.GetSettings().Returns(new FishingSettings { BoostMode = false, BoostModeRarityMultiplier = 1.0 });

            var baseRarity = new RarityProbability
            {
                Probabilities = new Dictionary<FishRarity, double>
                {
                    { FishRarity.Common, 50.0 },
                    { FishRarity.Uncommon, 30.0 },
                    { FishRarity.Rare, 15.0 },
                    { FishRarity.Epic, 4.0 },
                    { FishRarity.Legendary, 1.0 },
                    { FishRarity.Mythical, 0.0 }
                }
            };
            var boostedRarity = new RarityProbability
            {
                Probabilities = new Dictionary<FishRarity, double>
                {
                    { FishRarity.Common, 45.0 },
                    { FishRarity.Uncommon, 33.0 },
                    { FishRarity.Rare, 16.0 },
                    { FishRarity.Epic, 5.0 },
                    { FishRarity.Legendary, 1.0 },
                    { FishRarity.Mythical, 0.0 }
                }
            };

            analyticsService.CalculateRarityProbabilities(Arg.Any<bool>(), Arg.Any<double>(), Arg.Is<List<int>>(ids => ids.Contains(1)))
                .Returns(boostedRarity);
            analyticsService.CalculateRarityProbabilities(Arg.Any<bool>(), Arg.Any<double>(), Arg.Is<List<int>>(ids => !ids.Contains(1)))
                .Returns(baseRarity);

            analyticsService.CalculateCatchProbabilities(Arg.Any<bool>(), Arg.Any<double>(), Arg.Any<List<int>>())
                .Returns(new Dictionary<int, FishProbability>());

            return ctx;
        }

        [Fact]
        public async Task BuildComparisonRows_WhenReplacingEquippedItemWithGeneralRarityBoost_RendersRarityRows()
        {
            await using var ctx = CreateTestContext();

            // Arrange: Equipped item is a Rod with GeneralRarityBoost
            var equippedRod = new FishingShopItem
            {
                Id = 1,
                Name = "Bamboo Rod",
                EquipmentSlot = EquipmentSlot.Rod,
                BoostType = FishingBoostType.GeneralRarityBoost,
                BoostAmount = 0.05
            };

            // Preview item is a Rod without GeneralRarityBoost (e.g. WeightBoost)
            var newRod = new FishingShopItem
            {
                Id = 2,
                Name = "Heavy Rod",
                EquipmentSlot = EquipmentSlot.Rod,
                BoostType = FishingBoostType.WeightBoost,
                BoostAmount = 0.15
            };

            // Act: Render preview replacing equipped rod
            var cut = ctx.Render<ItemPurchasePreview>(parameters => parameters
                .Add(p => p.ShopItem, newRod)
                .Add(p => p.CurrentEquippedItems, new List<FishingShopItem> { equippedRod }));

            // Assert: Rarity comparison rows should be rendered because the equipped rod had GeneralRarityBoost
            var markup = cut.Markup;
            Assert.Contains("Common catch chance", markup);
            Assert.Contains("Uncommon catch chance", markup);
        }

        [Fact]
        public async Task BuildComparisonRows_WhenUnrelatedItemHasNoRarityBoost_DoesNotRenderRarityRows()
        {
            await using var ctx = CreateTestContext();

            // Arrange: Equipped item is a Rod with GeneralRarityBoost
            var equippedRod = new FishingShopItem
            {
                Id = 1,
                Name = "Bamboo Rod",
                EquipmentSlot = EquipmentSlot.Rod,
                BoostType = FishingBoostType.GeneralRarityBoost,
                BoostAmount = 0.05
            };

            // Preview item is a Hook (different slot) with StarBoost (no rarity boost involved)
            var newHook = new FishingShopItem
            {
                Id = 3,
                Name = "Standard Hook",
                EquipmentSlot = EquipmentSlot.Hook,
                BoostType = FishingBoostType.StarBoost,
                BoostAmount = 0.10
            };

            // Act: Render preview for hook while rod is equipped
            var cut = ctx.Render<ItemPurchasePreview>(parameters => parameters
                .Add(p => p.ShopItem, newHook)
                .Add(p => p.CurrentEquippedItems, new List<FishingShopItem> { equippedRod }));

            // Assert: Rarity comparison rows should NOT be rendered for unrelated item
            var markup = cut.Markup;
            Assert.DoesNotContain("Common catch chance", markup);
            Assert.DoesNotContain("Uncommon catch chance", markup);
            Assert.Contains("1-star catch chance", markup);
        }
    }
}
