using Microsoft.Extensions.Logging;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class FishingModifyHandler(
        IFishingService fishingService,
        IFishingShopService fishingShopService,
        ILogger<FishingModifyHandler> logger) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.FishingModify;

        public async Task ExecuteAsync(
            SubActionType subAction,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context = null,
            int subActionIndex = -1)
        {
            if (subAction is not FishingModifyType modify)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type provided to FishingModifyHandler");
            }

            if (modify.TargetType == FishingModifyTargetType.Fish)
            {
                await ModifyFishAsync(modify, variables, context, subActionIndex);
            }
            else
            {
                await ModifyShopItemAsync(modify, variables, context, subActionIndex);
            }
        }

        private async Task ModifyFishAsync(
            FishingModifyType modify,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context,
            int subActionIndex)
        {
            var target = VariableReplacer.ReplaceVariables(modify.TargetFish, variables).Trim();
            if (string.IsNullOrWhiteSpace(target))
            {
                throw new SubActionUserFacingException(modify, "Target fish is required.");
            }

            FishType? fish = null;
            if (int.TryParse(target, out var fishId) && fishId > 0)
            {
                fish = await fishingService.GetFishTypeById(fishId);
            }

            if (fish == null)
            {
                var allFish = await fishingService.GetAllFishTypes();
                fish = allFish.FirstOrDefault(f => string.Equals(f.Name, target, StringComparison.OrdinalIgnoreCase))
                    ?? allFish.FirstOrDefault(f => f.Id.ToString() == target);
            }

            if (fish == null)
            {
                throw new SubActionUserFacingException(modify, "Fish not found: {0}", target);
            }

            if (!string.IsNullOrWhiteSpace(modify.NewName))
            {
                var newName = VariableReplacer.ReplaceVariables(modify.NewName, variables).Trim();
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    fish.Name = newName;
                }
            }

            if (!string.IsNullOrWhiteSpace(modify.NewGold))
            {
                var newGoldStr = VariableReplacer.ReplaceVariables(modify.NewGold, variables).Trim();
                if (int.TryParse(newGoldStr, out var newGold))
                {
                    fish.BaseGold = Math.Max(0, newGold);
                }
            }

            if (modify.RarityMode == FishingModifyRarityMode.AutoFromGold)
            {
                var settings = await fishingService.GetSettings() ?? new FishingSettings();
                fish.Rarity = FishingRarityThresholdRules.CalculateRarityFromGold(fish.BaseGold, settings);
            }
            else if (modify.RarityMode == FishingModifyRarityMode.Manual && modify.ManualRarity.HasValue)
            {
                fish.Rarity = modify.ManualRarity.Value;
            }

            switch (modify.EnabledState)
            {
                case ItemEnabledStateAction.Enable:
                    fish.Enabled = true;
                    break;
                case ItemEnabledStateAction.Disable:
                    fish.Enabled = false;
                    break;
                case ItemEnabledStateAction.Toggle:
                    fish.Enabled = !fish.Enabled;
                    break;
            }

            await fishingService.UpdateFishType(fish);

            variables["modified_target_type"] = "Fish";
            variables["modified_fish_id"] = fish.Id.ToString();
            variables["modified_fish_name"] = fish.Name;
            variables["modified_fish_gold"] = fish.BaseGold.ToString();
            variables["modified_fish_rarity"] = fish.Rarity.ToString();
            variables["modified_fish_enabled"] = fish.Enabled.ToString().ToLowerInvariant();

            logger.LogInformation(
                "Modified fish '{FishName}' (ID: {FishId}): BaseGold={Gold}, Rarity={Rarity}, Enabled={Enabled}",
                fish.Name, fish.Id, fish.BaseGold, fish.Rarity, fish.Enabled);

            context?.LogMessage(
                subActionIndex,
                $"Modified fish '{fish.Name}' (ID: {fish.Id}): BaseGold={fish.BaseGold}, Rarity={fish.Rarity}, Enabled={fish.Enabled}.");
        }

        private async Task ModifyShopItemAsync(
            FishingModifyType modify,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context,
            int subActionIndex)
        {
            var target = VariableReplacer.ReplaceVariables(modify.TargetShopItem, variables).Trim();
            if (string.IsNullOrWhiteSpace(target))
            {
                throw new SubActionUserFacingException(modify, "Target shop item is required.");
            }

            FishingShopItem? item = null;
            if (int.TryParse(target, out var itemId) && itemId > 0)
            {
                item = await fishingShopService.GetShopItemById(itemId);
            }

            if (item == null)
            {
                var allItems = await fishingShopService.GetAllShopItems();
                item = allItems.FirstOrDefault(i => string.Equals(i.Name, target, StringComparison.OrdinalIgnoreCase))
                    ?? allItems.FirstOrDefault(i => i.Id.ToString() == target);
            }

            if (item == null)
            {
                throw new SubActionUserFacingException(modify, "Shop item not found: {0}", target);
            }

            if (!string.IsNullOrWhiteSpace(modify.NewName))
            {
                var newName = VariableReplacer.ReplaceVariables(modify.NewName, variables).Trim();
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    item.Name = newName;
                }
            }

            if (!string.IsNullOrWhiteSpace(modify.NewCost))
            {
                var newCostStr = VariableReplacer.ReplaceVariables(modify.NewCost, variables).Trim();
                if (int.TryParse(newCostStr, out var newCost))
                {
                    item.Cost = Math.Max(0, newCost);
                }
            }

            if (!string.IsNullOrWhiteSpace(modify.NewDescription))
            {
                item.Description = VariableReplacer.ReplaceVariables(modify.NewDescription, variables).Trim();
            }

            if (modify.NewBoostType.HasValue)
            {
                item.BoostType = modify.NewBoostType.Value;
            }

            if (!string.IsNullOrWhiteSpace(modify.NewBoostAmount))
            {
                var boostStr = VariableReplacer.ReplaceVariables(modify.NewBoostAmount, variables).Trim();
                if (double.TryParse(boostStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var boost) ||
                    double.TryParse(boostStr, out boost))
                {
                    item.BoostAmount = Math.Round(Math.Clamp(boost, -0.8, 5.0), 2, MidpointRounding.AwayFromZero);
                }
            }

            if (!string.IsNullOrWhiteSpace(modify.NewTargetFish))
            {
                var targetFish = VariableReplacer.ReplaceVariables(modify.NewTargetFish, variables).Trim();
                if (targetFish.Equals("none", StringComparison.OrdinalIgnoreCase) || targetFish == "0")
                {
                    item.TargetFishTypeId = null;
                }
                else if (int.TryParse(targetFish, out var targetFishId) && targetFishId > 0)
                {
                    item.TargetFishTypeId = targetFishId;
                }
                else
                {
                    var allFish = await fishingService.GetAllFishTypes();
                    var matchedFish = allFish.FirstOrDefault(f => string.Equals(f.Name, targetFish, StringComparison.OrdinalIgnoreCase))
                        ?? allFish.FirstOrDefault(f => f.Id.ToString() == targetFish);
                    if (matchedFish != null)
                    {
                        item.TargetFishTypeId = matchedFish.Id;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(modify.NewTargetCategory))
            {
                var targetCat = VariableReplacer.ReplaceVariables(modify.NewTargetCategory, variables).Trim();
                item.TargetCategory = targetCat.Equals("none", StringComparison.OrdinalIgnoreCase) ? null : targetCat;
            }

            if (!string.IsNullOrWhiteSpace(modify.NewEquipmentSlot) &&
                !modify.NewEquipmentSlot.Equals("KeepCurrent", StringComparison.OrdinalIgnoreCase))
            {
                if (modify.NewEquipmentSlot.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    item.EquipmentSlot = null;
                }
                else if (Enum.TryParse<EquipmentSlot>(modify.NewEquipmentSlot, true, out var parsedSlot))
                {
                    item.EquipmentSlot = parsedSlot;
                }
            }

            if (!string.IsNullOrWhiteSpace(modify.NewMaxUses))
            {
                var usesStr = VariableReplacer.ReplaceVariables(modify.NewMaxUses, variables).Trim();
                if (usesStr.Equals("unlimited", StringComparison.OrdinalIgnoreCase) ||
                    usesStr.Equals("none", StringComparison.OrdinalIgnoreCase) ||
                    usesStr == "0" ||
                    usesStr == "-1")
                {
                    item.MaxUses = null;
                }
                else if (int.TryParse(usesStr, out var parsedUses) && parsedUses > 0)
                {
                    item.MaxUses = parsedUses;
                }
            }

            switch (modify.AdminOnlyState)
            {
                case ItemEnabledStateAction.Enable:
                    item.IsAdminOnly = true;
                    break;
                case ItemEnabledStateAction.Disable:
                    item.IsAdminOnly = false;
                    break;
                case ItemEnabledStateAction.Toggle:
                    item.IsAdminOnly = !item.IsAdminOnly;
                    break;
            }

            switch (modify.EnabledState)
            {
                case ItemEnabledStateAction.Enable:
                    item.Enabled = true;
                    break;
                case ItemEnabledStateAction.Disable:
                    item.Enabled = false;
                    break;
                case ItemEnabledStateAction.Toggle:
                    item.Enabled = !item.Enabled;
                    break;
            }

            await fishingShopService.UpdateShopItem(item);

            variables["modified_target_type"] = "ShopItem";
            variables["modified_shop_item_id"] = item.Id.ToString();
            variables["modified_shop_item_name"] = item.Name;
            variables["modified_shop_item_cost"] = item.Cost.ToString();
            variables["modified_shop_item_boost_type"] = item.BoostType.ToString();
            variables["modified_shop_item_boost_amount"] = item.BoostAmount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            variables["modified_shop_item_equipment_slot"] = item.EquipmentSlot?.ToString() ?? "None";
            variables["modified_shop_item_max_uses"] = item.MaxUses?.ToString() ?? "Unlimited";
            variables["modified_shop_item_admin_only"] = item.IsAdminOnly.ToString().ToLowerInvariant();
            variables["modified_shop_item_enabled"] = item.Enabled.ToString().ToLowerInvariant();

            logger.LogInformation(
                "Modified fishing shop item '{ItemName}' (ID: {ItemId}): Cost={Cost}, Enabled={Enabled}",
                item.Name, item.Id, item.Cost, item.Enabled);

            context?.LogMessage(
                subActionIndex,
                $"Modified fishing shop item '{item.Name}' (ID: {item.Id}): Cost={item.Cost}, Enabled={item.Enabled}.");
        }
    }
}
