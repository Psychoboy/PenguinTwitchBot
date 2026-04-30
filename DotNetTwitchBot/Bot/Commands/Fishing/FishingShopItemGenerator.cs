using DotNetTwitchBot.Bot.Core.Database;
using DotNetTwitchBot.Bot.Models.Fishing;
using Microsoft.EntityFrameworkCore;

namespace DotNetTwitchBot.Bot.Commands.Fishing
{
    /// <summary>
    /// Generates default shop items for the fishing system.
    /// Separated from FishingShopService for better organization and maintainability.
    /// </summary>
    public class FishingShopItemGenerator
    {
        public async Task<int> GenerateDefaultItems(ApplicationDbContext context, bool updateExisting = false)
        {
            var existingItems = await context.FishingShopItems.ToListAsync();
            var itemsToAdd = new List<FishingShopItem>();

            var existingNames = new HashSet<string>(
                existingItems.Select(i => i.Name),
                StringComparer.OrdinalIgnoreCase
            );

            bool ItemExists(string name) =>
                (!updateExisting && existingNames.Contains(name)) ||
                itemsToAdd.Any(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            // Add equipment items
            AddRods(itemsToAdd, ItemExists);
            AddReels(itemsToAdd, ItemExists);
            AddLines(itemsToAdd, ItemExists);
            AddHooks(itemsToAdd, ItemExists);
            AddTackleBoxes(itemsToAdd, ItemExists);
            AddNets(itemsToAdd, ItemExists);

            // Add consumables
            AddBaits(itemsToAdd, ItemExists);
            AddLures(itemsToAdd, ItemExists);

            // Add fish-specific items
            await AddFishSpecificItems(context, itemsToAdd, ItemExists);

            var changedCount = 0;

            if (updateExisting && itemsToAdd.Any())
            {
                var addList = new List<FishingShopItem>();
                var existingByName = existingItems.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);

                foreach (var template in itemsToAdd)
                {
                    if (existingByName.TryGetValue(template.Name, out var existing))
                    {
                        if (ApplyTemplate(existing, template))
                        {
                            changedCount++;
                        }
                    }
                    else
                    {
                        addList.Add(template);
                    }
                }

                if (addList.Any())
                {
                    context.FishingShopItems.AddRange(addList);
                    changedCount += addList.Count;
                }
            }
            else if (itemsToAdd.Any())
            {
                context.FishingShopItems.AddRange(itemsToAdd);
                changedCount += itemsToAdd.Count;
            }

            if (changedCount > 0)
            {
                await context.SaveChangesAsync();
            }

            return changedCount;
        }

        private static bool ApplyTemplate(FishingShopItem existing, FishingShopItem template)
        {
            var changed = false;

            if (existing.Description != template.Description) { existing.Description = template.Description; changed = true; }
            if (existing.Cost != template.Cost) { existing.Cost = template.Cost; changed = true; }
            if (existing.BoostType != template.BoostType) { existing.BoostType = template.BoostType; changed = true; }
            if (existing.BoostAmount != template.BoostAmount) { existing.BoostAmount = template.BoostAmount; changed = true; }
            if (existing.BoostType2 != template.BoostType2) { existing.BoostType2 = template.BoostType2; changed = true; }
            if (existing.BoostAmount2 != template.BoostAmount2) { existing.BoostAmount2 = template.BoostAmount2; changed = true; }
            if (existing.BoostType3 != template.BoostType3) { existing.BoostType3 = template.BoostType3; changed = true; }
            if (existing.BoostAmount3 != template.BoostAmount3) { existing.BoostAmount3 = template.BoostAmount3; changed = true; }
            if (existing.TargetFishTypeId != template.TargetFishTypeId) { existing.TargetFishTypeId = template.TargetFishTypeId; changed = true; }
            if (existing.Enabled != template.Enabled) { existing.Enabled = template.Enabled; changed = true; }
            if (existing.EquipmentSlot != template.EquipmentSlot) { existing.EquipmentSlot = template.EquipmentSlot; changed = true; }
            if (existing.MaxUses != template.MaxUses) { existing.MaxUses = template.MaxUses; changed = true; }
            if (existing.IsConsumable != template.IsConsumable) { existing.IsConsumable = template.IsConsumable; changed = true; }
            if (existing.IsAdminOnly != template.IsAdminOnly) { existing.IsAdminOnly = template.IsAdminOnly; changed = true; }

            return changed;
        }

        private void AddRods(List<FishingShopItem> items, Func<string, bool> itemExists)
        {
            if (!itemExists("Bamboo Rod"))
                items.Add(new FishingShopItem { Name = "Bamboo Rod", Description = "A basic bamboo fishing rod (+5% rare fish)", Cost = 150, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.05, EquipmentSlot = EquipmentSlot.Rod, IsConsumable = false, Enabled = true });

            if (!itemExists("Fiberglass Rod"))
                items.Add(new FishingShopItem { Name = "Fiberglass Rod", Description = "Flexible and durable (+10% rare fish)", Cost = 400, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.10, EquipmentSlot = EquipmentSlot.Rod, IsConsumable = false, Enabled = true });

            if (!itemExists("Carbon Fiber Rod"))
                items.Add(new FishingShopItem { Name = "Carbon Fiber Rod", Description = "Lightweight and strong (+15% rare fish)", Cost = 1000, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.15, EquipmentSlot = EquipmentSlot.Rod, IsConsumable = false, Enabled = true });

            if (!itemExists("Legendary Rod"))
                items.Add(new FishingShopItem { Name = "Legendary Rod", Description = "The stuff of legends (+25% rare fish)", Cost = 2500, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.25, EquipmentSlot = EquipmentSlot.Rod, IsConsumable = false, Enabled = true });
        }

        private void AddReels(List<FishingShopItem> items, Func<string, bool> itemExists)
        {
            if (!itemExists("Basic Reel"))
                items.Add(new FishingShopItem { Name = "Basic Reel", Description = "Simple spinning reel (+5% star quality)", Cost = 200, BoostType = FishingBoostType.StarBoost, BoostAmount = 0.05, EquipmentSlot = EquipmentSlot.Reel, IsConsumable = false, Enabled = true });

            if (!itemExists("Precision Reel"))
                items.Add(new FishingShopItem { Name = "Precision Reel", Description = "Smooth drag system (+10% star quality)", Cost = 500, BoostType = FishingBoostType.StarBoost, BoostAmount = 0.10, EquipmentSlot = EquipmentSlot.Reel, IsConsumable = false, Enabled = true });

            if (!itemExists("Professional Reel"))
                items.Add(new FishingShopItem { Name = "Professional Reel", Description = "Tournament-grade precision (+15% star quality)", Cost = 1200, BoostType = FishingBoostType.StarBoost, BoostAmount = 0.15, EquipmentSlot = EquipmentSlot.Reel, IsConsumable = false, Enabled = true });

            if (!itemExists("Master Reel"))
                items.Add(new FishingShopItem { Name = "Master Reel", Description = "Ultimate control and smoothness (+20% star quality)", Cost = 3000, BoostType = FishingBoostType.StarBoost, BoostAmount = 0.20, EquipmentSlot = EquipmentSlot.Reel, IsConsumable = false, Enabled = true });
        }

        private void AddLines(List<FishingShopItem> items, Func<string, bool> itemExists)
        {
            if (!itemExists("Monofilament Line"))
                items.Add(new FishingShopItem { Name = "Monofilament Line", Description = "Basic fishing line (+10% weight)", Cost = 175, BoostType = FishingBoostType.WeightBoost, BoostAmount = 0.10, EquipmentSlot = EquipmentSlot.Line, IsConsumable = false, Enabled = true });

            if (!itemExists("Braided Line"))
                items.Add(new FishingShopItem { Name = "Braided Line", Description = "High strength, low stretch (+20% weight)", Cost = 450, BoostType = FishingBoostType.WeightBoost, BoostAmount = 0.20, EquipmentSlot = EquipmentSlot.Line, IsConsumable = false, Enabled = true });

            if (!itemExists("Fluorocarbon Line"))
                items.Add(new FishingShopItem { Name = "Fluorocarbon Line", Description = "Nearly invisible, very strong (+30% weight)", Cost = 1100, BoostType = FishingBoostType.WeightBoost, BoostAmount = 0.30, EquipmentSlot = EquipmentSlot.Line, IsConsumable = false, Enabled = true });

            if (!itemExists("Titanium Wire"))
                items.Add(new FishingShopItem { Name = "Titanium Wire", Description = "Unbreakable fishing wire (+45% weight)", Cost = 2800, BoostType = FishingBoostType.WeightBoost, BoostAmount = 0.45, EquipmentSlot = EquipmentSlot.Line, IsConsumable = false, Enabled = true });
        }

        private void AddHooks(List<FishingShopItem> items, Func<string, bool> itemExists)
        {
            if (!itemExists("Standard Hook"))
                items.Add(new FishingShopItem { Name = "Standard Hook", Description = "Reliable J-hook (+5% star quality)", Cost = 150, BoostType = FishingBoostType.StarBoost, BoostAmount = 0.05, EquipmentSlot = EquipmentSlot.Hook, IsConsumable = false, Enabled = true });

            if (!itemExists("Circle Hook"))
                items.Add(new FishingShopItem { Name = "Circle Hook", Description = "Self-setting design (+10% star quality)", Cost = 400, BoostType = FishingBoostType.StarBoost, BoostAmount = 0.10, EquipmentSlot = EquipmentSlot.Hook, IsConsumable = false, Enabled = true });

            if (!itemExists("Treble Hook"))
                items.Add(new FishingShopItem { Name = "Treble Hook", Description = "Triple the catching power (+15% star quality)", Cost = 1000, BoostType = FishingBoostType.StarBoost, BoostAmount = 0.15, EquipmentSlot = EquipmentSlot.Hook, IsConsumable = false, Enabled = true });

            if (!itemExists("Diamond Hook"))
                items.Add(new FishingShopItem { Name = "Diamond Hook", Description = "Razor-sharp perfection (+22% star quality)", Cost = 2500, BoostType = FishingBoostType.StarBoost, BoostAmount = 0.22, EquipmentSlot = EquipmentSlot.Hook, IsConsumable = false, Enabled = true });
        }

        private void AddTackleBoxes(List<FishingShopItem> items, Func<string, bool> itemExists)
        {
            if (!itemExists("Basic Tackle Box"))
                items.Add(new FishingShopItem { Name = "Basic Tackle Box", Description = "Organized storage (+5% rarity, +5% weight)", Cost = 300, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.05, BoostType2 = FishingBoostType.WeightBoost, BoostAmount2 = 0.05, EquipmentSlot = EquipmentSlot.TackleBox, IsConsumable = false, Enabled = true });

            if (!itemExists("Pro Tackle Box"))
                items.Add(new FishingShopItem { Name = "Pro Tackle Box", Description = "Everything you need (+10% rarity, +10% weight)", Cost = 800, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.10, BoostType2 = FishingBoostType.WeightBoost, BoostAmount2 = 0.10, EquipmentSlot = EquipmentSlot.TackleBox, IsConsumable = false, Enabled = true });

            if (!itemExists("Master Tackle Box"))
                items.Add(new FishingShopItem { Name = "Master Tackle Box", Description = "Complete arsenal (+15% rarity, +15% weight)", Cost = 2000, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.15, BoostType2 = FishingBoostType.WeightBoost, BoostAmount2 = 0.15, EquipmentSlot = EquipmentSlot.TackleBox, IsConsumable = false, Enabled = true });
        }

        private void AddNets(List<FishingShopItem> items, Func<string, bool> itemExists)
        {
            if (!itemExists("Landing Net"))
                items.Add(new FishingShopItem { Name = "Landing Net", Description = "Helps land bigger fish (+15% weight)", Cost = 350, BoostType = FishingBoostType.WeightBoost, BoostAmount = 0.15, EquipmentSlot = EquipmentSlot.Net, IsConsumable = false, Enabled = true });

            if (!itemExists("Knotless Net"))
                items.Add(new FishingShopItem { Name = "Knotless Net", Description = "Gentle on fish, strong hold (+25% weight)", Cost = 900, BoostType = FishingBoostType.WeightBoost, BoostAmount = 0.25, EquipmentSlot = EquipmentSlot.Net, IsConsumable = false, Enabled = true });

            if (!itemExists("Tournament Net"))
                items.Add(new FishingShopItem { Name = "Tournament Net", Description = "Professional-grade netting (+35% weight)", Cost = 2200, BoostType = FishingBoostType.WeightBoost, BoostAmount = 0.35, EquipmentSlot = EquipmentSlot.Net, IsConsumable = false, Enabled = true });
        }

        private void AddBaits(List<FishingShopItem> items, Func<string, bool> itemExists)
        {
            if (!itemExists("Worms"))
                items.Add(new FishingShopItem { Name = "Worms", Description = "Classic bait for all fish (+15% rarity, 5 uses)", Cost = 75, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.15, EquipmentSlot = EquipmentSlot.Bait, IsConsumable = true, MaxUses = 5, Enabled = true });

            if (!itemExists("Minnows"))
                items.Add(new FishingShopItem { Name = "Minnows", Description = "Live bait for predators (+20% rarity, 5 uses)", Cost = 120, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.20, EquipmentSlot = EquipmentSlot.Bait, IsConsumable = true, MaxUses = 5, Enabled = true });

            if (!itemExists("Premium Bait"))
                items.Add(new FishingShopItem { Name = "Premium Bait", Description = "Irresistible to rare fish (+30% rarity, 5 uses)", Cost = 200, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.30, EquipmentSlot = EquipmentSlot.Bait, IsConsumable = true, MaxUses = 5, Enabled = true });

            if (!itemExists("Golden Bait"))
                items.Add(new FishingShopItem { Name = "Golden Bait", Description = "Legendary attractant (+50% rarity, 3 uses)", Cost = 350, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.50, EquipmentSlot = EquipmentSlot.Bait, IsConsumable = true, MaxUses = 3, Enabled = true });
        }

        private void AddLures(List<FishingShopItem> items, Func<string, bool> itemExists)
        {
            if (!itemExists("Spoon Lure"))
                items.Add(new FishingShopItem { Name = "Spoon Lure", Description = "Flashy wobbling action (+12% rarity, 8 uses)", Cost = 90, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.12, EquipmentSlot = EquipmentSlot.Lure, IsConsumable = true, MaxUses = 8, Enabled = true });

            if (!itemExists("Crankbait"))
                items.Add(new FishingShopItem { Name = "Crankbait", Description = "Deep diving lure (+18% rarity, 7 uses)", Cost = 140, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.18, EquipmentSlot = EquipmentSlot.Lure, IsConsumable = true, MaxUses = 7, Enabled = true });

            if (!itemExists("Jerkbait"))
                items.Add(new FishingShopItem { Name = "Jerkbait", Description = "Erratic swimming motion (+25% rarity, 6 uses)", Cost = 220, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.25, EquipmentSlot = EquipmentSlot.Lure, IsConsumable = true, MaxUses = 6, Enabled = true });

            if (!itemExists("TopWater Popper"))
                items.Add(new FishingShopItem { Name = "TopWater Popper", Description = "Surface explosion action (+35% rarity, 5 uses)", Cost = 320, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.35, EquipmentSlot = EquipmentSlot.Lure, IsConsumable = true, MaxUses = 5, Enabled = true });

            if (!itemExists("Swimbait"))
                items.Add(new FishingShopItem { Name = "Swimbait", Description = "Realistic swimming lure (+45% rarity, 5 uses)", Cost = 400, BoostType = FishingBoostType.GeneralRarityBoost, BoostAmount = 0.45, EquipmentSlot = EquipmentSlot.Lure, IsConsumable = true, MaxUses = 5, Enabled = true });
        }

        private async Task AddFishSpecificItems(ApplicationDbContext context, List<FishingShopItem> items, Func<string, bool> itemExists)
        {
            var allFish = await context.FishTypes.Where(f => f.Enabled).ToListAsync();
            var targetFish = allFish.Where(f => f.Rarity >= FishRarity.Rare).ToList();

            foreach (var fish in targetFish)
            {
                // Fish-Specific Bait
                var baitName = $"{fish.Name} Bait";
                if (!itemExists(baitName))
                {
                    var baitCost = fish.Rarity switch
                    {
                        FishRarity.Legendary => 800,
                        FishRarity.Epic => 450,
                        FishRarity.Rare => 250,
                        _ => 150
                    };

                    var baitBoost = fish.Rarity switch
                    {
                        FishRarity.Legendary => 3.5,   // 350% boost
                        FishRarity.Epic => 2.5,        // 250% boost
                        FishRarity.Rare => 1.8,        // 180% boost
                        _ => 1.0
                    };

                    var baitUses = fish.Rarity switch
                    {
                        FishRarity.Legendary => 8,
                        FishRarity.Epic => 10,
                        _ => 12
                    };

                    items.Add(new FishingShopItem
                    {
                        Name = baitName,
                        Description = $"Specialized bait for {fish.Name} ({baitUses} uses)",
                        Cost = baitCost,
                        BoostType = FishingBoostType.SpecificFishBoost,
                        BoostAmount = baitBoost,
                        TargetFishTypeId = fish.Id,
                        IsConsumable = true,
                        MaxUses = baitUses,
                        Enabled = true,
                        EquipmentSlot = EquipmentSlot.Bait
                    });
                }

                // Fish-Specific Lure
                var lureName = $"{fish.Name} Lure";
                if (!itemExists(lureName))
                {
                    var lureCost = fish.Rarity switch
                    {
                        FishRarity.Legendary => 1000,
                        FishRarity.Epic => 600,
                        FishRarity.Rare => 350,
                        _ => 200
                    };

                    var lureBoost = fish.Rarity switch
                    {
                        FishRarity.Legendary => 4.5,   // 450% boost  
                        FishRarity.Epic => 3.0,        // 300% boost
                        FishRarity.Rare => 2.2,        // 220% boost
                        _ => 1.2
                    };

                    var lureUses = fish.Rarity switch
                    {
                        FishRarity.Legendary => 10,
                        FishRarity.Epic => 12,
                        _ => 15
                    };

                    items.Add(new FishingShopItem
                    {
                        Name = lureName,
                        Description = $"Premium lure designed for {fish.Name} ({lureUses} uses)",
                        Cost = lureCost,
                        BoostType = FishingBoostType.SpecificFishBoost,
                        BoostAmount = lureBoost,
                        TargetFishTypeId = fish.Id,
                        IsConsumable = true,
                        MaxUses = lureUses,
                        Enabled = true,
                        EquipmentSlot = EquipmentSlot.Lure
                    });
                }
            }
        }
    }
}
