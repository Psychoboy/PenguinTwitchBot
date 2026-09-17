using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;
using PenguinTwitchBot.Database.Bot.Models.Fishing;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    public enum FishingModifyTargetType
    {
        Fish = 0,
        ShopItem = 1
    }

    public enum FishingModifyRarityMode
    {
        KeepCurrent = 0,
        AutoFromGold = 1,
        Manual = 2
    }

    public enum ItemEnabledStateAction
    {
        KeepCurrent = 0,
        Enable = 1,
        Disable = 2,
        Toggle = 3
    }

    [SubActionMetadata(
        displayName: "Fishing - Modify Fish / Shop Item",
        description: "Modify a fish or fishing shop item's attributes, gold/cost, modifiers, enabled state, and rarity",
        icon: "mdi-fish",
        color: "Primary",
        tableName: "subactions_fishingmodify")]
    public class FishingModifyType : SubActionType, ISubActionUIProvider
    {
        public FishingModifyType()
        {
            SubActionTypes = SubActionTypes.FishingModify;
        }

        public FishingModifyTargetType TargetType { get; set; } = FishingModifyTargetType.Fish;
        public string TargetFish { get; set; } = string.Empty;
        public string TargetShopItem { get; set; } = string.Empty;
        public string NewName { get; set; } = string.Empty;
        public string NewGold { get; set; } = string.Empty;
        public string NewCost { get; set; } = string.Empty;
        public string NewDescription { get; set; } = string.Empty;
        public FishingModifyRarityMode RarityMode { get; set; } = FishingModifyRarityMode.KeepCurrent;
        public FishRarity? ManualRarity { get; set; } = FishRarity.Common;
        public ItemEnabledStateAction EnabledState { get; set; } = ItemEnabledStateAction.KeepCurrent;

        // Shop Item Modifiers
        public FishingBoostType? NewBoostType { get; set; }
        public string NewBoostAmount { get; set; } = string.Empty;
        public string NewTargetFish { get; set; } = string.Empty;
        public string NewTargetCategory { get; set; } = string.Empty;
        public string NewEquipmentSlot { get; set; } = string.Empty;
        public string NewMaxUses { get; set; } = string.Empty;
        public ItemEnabledStateAction AdminOnlyState { get; set; } = ItemEnabledStateAction.KeepCurrent;

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            var fields = new List<SubActionUIField>
            {
                new()
                {
                    PropertyName = nameof(TargetType),
                    Label = "Target Type",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    SelectOptions =
                    [
                        new() { Name = "Fish", Value = nameof(FishingModifyTargetType.Fish) },
                        new() { Name = "Fishing Shop Item", Value = nameof(FishingModifyTargetType.ShopItem) }
                    ],
                    HelperText = "Select whether you want to modify a fish or a fishing shop item."
                }
            };

            if (TargetType == FishingModifyTargetType.Fish)
            {
                fields.Add(new()
                {
                    PropertyName = nameof(TargetFish),
                    Label = "Fish to Modify",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    AllowCustomValue = true,
                    SelectOptions = [],
                    HelperText = "Select a fish from the list or enter a name, ID, or variable (e.g. %fish_type% or %fish_type_id%)."
                });

                fields.Add(new()
                {
                    PropertyName = nameof(NewName),
                    Label = "New Fish Name",
                    FieldType = UIFieldType.Text,
                    HelperText = "Optional. Leave blank to keep current name. Variables supported."
                });

                fields.Add(new()
                {
                    PropertyName = nameof(NewGold),
                    Label = "New Base Gold",
                    FieldType = UIFieldType.Text,
                    HelperText = "Optional. Leave blank to keep current gold. Accepts numbers or variables like %gold%."
                });

                fields.Add(new()
                {
                    PropertyName = nameof(RarityMode),
                    Label = "Rarity Setting",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    SelectOptions =
                    [
                        new() { Name = "Keep Current (Do not change rarity)", Value = nameof(FishingModifyRarityMode.KeepCurrent) },
                        new() { Name = "Auto (Calculate from Gold based on threshold rules)", Value = nameof(FishingModifyRarityMode.AutoFromGold) },
                        new() { Name = "Manual (Set explicit rarity)", Value = nameof(FishingModifyRarityMode.Manual) }
                    ],
                    HelperText = "Choose how to update rarity. If Auto, rarity recalculates from gold value based on rules."
                });

                if (RarityMode == FishingModifyRarityMode.Manual)
                {
                    fields.Add(new()
                    {
                        PropertyName = nameof(ManualRarity),
                        Label = "Manual Rarity Tier",
                        FieldType = UIFieldType.Select,
                        Required = true,
                        Options = Enum.GetNames<FishRarity>(),
                        HelperText = "The explicit rarity tier to apply."
                    });
                }

                fields.Add(new()
                {
                    PropertyName = nameof(EnabledState),
                    Label = "Fish Enabled State",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    SelectOptions =
                    [
                        new() { Name = "Keep Current (No change)", Value = nameof(ItemEnabledStateAction.KeepCurrent) },
                        new() { Name = "Enable Fish", Value = nameof(ItemEnabledStateAction.Enable) },
                        new() { Name = "Disable Fish", Value = nameof(ItemEnabledStateAction.Disable) },
                        new() { Name = "Toggle Enabled State", Value = nameof(ItemEnabledStateAction.Toggle) }
                    ],
                    HelperText = "Control whether the fish is enabled, disabled, or kept unchanged."
                });
            }
            else
            {
                fields.Add(new()
                {
                    PropertyName = nameof(TargetShopItem),
                    Label = "Shop Item to Modify",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    AllowCustomValue = true,
                    SelectOptions = [],
                    HelperText = "Select a shop item from the list or enter a name, ID, or variable."
                });

                fields.Add(new()
                {
                    PropertyName = nameof(NewName),
                    Label = "New Item Name",
                    FieldType = UIFieldType.Text,
                    HelperText = "Optional. Leave blank to keep current name. Variables supported."
                });

                fields.Add(new()
                {
                    PropertyName = nameof(NewCost),
                    Label = "New Cost / Price",
                    FieldType = UIFieldType.Text,
                    HelperText = "Optional. Leave blank to keep current cost. Accepts numbers or variables like %cost%."
                });

                fields.Add(new()
                {
                    PropertyName = nameof(NewDescription),
                    Label = "New Description",
                    FieldType = UIFieldType.TextArea,
                    Lines = 3,
                    HelperText = "Optional. Leave blank to keep current description. Variables supported."
                });

                fields.Add(new()
                {
                    PropertyName = nameof(NewBoostType),
                    Label = "New Boost Type",
                    FieldType = UIFieldType.Select,
                    SelectOptions =
                    [
                        new() { Name = "Keep Current (No change)", Value = "" },
                        new() { Name = "Rarity Boost", Value = nameof(FishingBoostType.GeneralRarityBoost) },
                        new() { Name = "Specific Fish Boost", Value = nameof(FishingBoostType.SpecificFishBoost) },
                        new() { Name = "Specific Category Boost", Value = nameof(FishingBoostType.SpecificCategoryBoost) },
                        new() { Name = "Weight Boost", Value = nameof(FishingBoostType.WeightBoost) },
                        new() { Name = "Star Boost", Value = nameof(FishingBoostType.StarBoost) }
                    ],
                    HelperText = "Optional. Leave as Keep Current to retain the item's current boost modifier."
                });

                fields.Add(new()
                {
                    PropertyName = nameof(NewBoostAmount),
                    Label = "New Boost Amount",
                    FieldType = UIFieldType.Text,
                    HelperText = "Optional. Enter decimal (e.g. 0.05 for +5%, -0.1 for -10%). Variables supported."
                });

                if (NewBoostType == FishingBoostType.SpecificFishBoost)
                {
                    fields.Add(new()
                    {
                        PropertyName = nameof(NewTargetFish),
                        Label = "Target Fish (for Specific Fish Boost)",
                        FieldType = UIFieldType.Select,
                        AllowCustomValue = true,
                        SelectOptions = [],
                        HelperText = "Select target fish or enter fish name/ID/variable."
                    });
                }

                if (NewBoostType == FishingBoostType.SpecificCategoryBoost)
                {
                    fields.Add(new()
                    {
                        PropertyName = nameof(NewTargetCategory),
                        Label = "Target Category (for Specific Category Boost)",
                        FieldType = UIFieldType.Select,
                        AllowCustomValue = true,
                        SelectOptions = [],
                        HelperText = "Select or enter target category name/variable."
                    });
                }

                fields.Add(new()
                {
                    PropertyName = nameof(NewEquipmentSlot),
                    Label = "New Equipment Slot",
                    FieldType = UIFieldType.Select,
                    SelectOptions =
                    [
                        new() { Name = "Keep Current (No change)", Value = "" },
                        new() { Name = "None (Permanent Boost)", Value = "None" },
                        new() { Name = "Rod", Value = nameof(EquipmentSlot.Rod) },
                        new() { Name = "Reel", Value = nameof(EquipmentSlot.Reel) },
                        new() { Name = "Line", Value = nameof(EquipmentSlot.Line) },
                        new() { Name = "Hook", Value = nameof(EquipmentSlot.Hook) },
                        new() { Name = "Bait", Value = nameof(EquipmentSlot.Bait) },
                        new() { Name = "Lure", Value = nameof(EquipmentSlot.Lure) },
                        new() { Name = "Tackle Box", Value = nameof(EquipmentSlot.TackleBox) },
                        new() { Name = "Net", Value = nameof(EquipmentSlot.Net) },
                        new() { Name = "Special", Value = nameof(EquipmentSlot.Special) }
                    ],
                    HelperText = "Optional. Assign to an equipment slot or set to None."
                });

                fields.Add(new()
                {
                    PropertyName = nameof(NewMaxUses),
                    Label = "New Max Uses",
                    FieldType = UIFieldType.Text,
                    HelperText = "Optional. Leave blank to keep current, enter number (e.g. 10), or 'unlimited'."
                });

                fields.Add(new()
                {
                    PropertyName = nameof(AdminOnlyState),
                    Label = "Admin Only Setting",
                    FieldType = UIFieldType.Select,
                    SelectOptions =
                    [
                        new() { Name = "Keep Current (No change)", Value = nameof(ItemEnabledStateAction.KeepCurrent) },
                        new() { Name = "Admin Only (Hide from player shop)", Value = nameof(ItemEnabledStateAction.Enable) },
                        new() { Name = "Public (Show in player shop)", Value = nameof(ItemEnabledStateAction.Disable) },
                        new() { Name = "Toggle Admin Only", Value = nameof(ItemEnabledStateAction.Toggle) }
                    ],
                    HelperText = "Control whether the item is restricted to admins."
                });

                fields.Add(new()
                {
                    PropertyName = nameof(EnabledState),
                    Label = "Shop Item Enabled State",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    SelectOptions =
                    [
                        new() { Name = "Keep Current (No change)", Value = nameof(ItemEnabledStateAction.KeepCurrent) },
                        new() { Name = "Enable Item", Value = nameof(ItemEnabledStateAction.Enable) },
                        new() { Name = "Disable Item", Value = nameof(ItemEnabledStateAction.Disable) },
                        new() { Name = "Toggle Enabled State", Value = nameof(ItemEnabledStateAction.Toggle) }
                    ],
                    HelperText = "Control whether the shop item is enabled, disabled, or kept unchanged."
                });
            }

            fields.Add(new()
            {
                PropertyName = nameof(Enabled),
                Label = "Subaction Enabled",
                FieldType = UIFieldType.Switch,
                SwitchColor = "Success",
                HelperText = "Controls whether this subaction itself is active."
            });

            return fields;
        }

        public Dictionary<string, object?> GetValues()
        {
            return new Dictionary<string, object?>
            {
                { nameof(TargetType), TargetType.ToString() },
                { nameof(TargetFish), TargetFish },
                { nameof(TargetShopItem), TargetShopItem },
                { nameof(NewName), NewName },
                { nameof(NewGold), NewGold },
                { nameof(NewCost), NewCost },
                { nameof(NewDescription), NewDescription },
                { nameof(RarityMode), RarityMode.ToString() },
                { nameof(ManualRarity), ManualRarity?.ToString() ?? nameof(FishRarity.Common) },
                { nameof(EnabledState), EnabledState.ToString() },
                { nameof(NewBoostType), NewBoostType?.ToString() ?? string.Empty },
                { nameof(NewBoostAmount), NewBoostAmount },
                { nameof(NewTargetFish), NewTargetFish },
                { nameof(NewTargetCategory), NewTargetCategory },
                { nameof(NewEquipmentSlot), NewEquipmentSlot },
                { nameof(NewMaxUses), NewMaxUses },
                { nameof(AdminOnlyState), AdminOnlyState.ToString() },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(TargetType), out var targetType) && targetType != null)
            {
                if (Enum.TryParse<FishingModifyTargetType>(targetType.ToString(), true, out var parsedTargetType))
                {
                    TargetType = parsedTargetType;
                }
            }

            if (values.TryGetValue(nameof(TargetFish), out var targetFish))
            {
                TargetFish = targetFish?.ToString() ?? string.Empty;
            }

            if (values.TryGetValue(nameof(TargetShopItem), out var targetShopItem))
            {
                TargetShopItem = targetShopItem?.ToString() ?? string.Empty;
            }

            if (values.TryGetValue(nameof(NewName), out var newName))
            {
                NewName = newName?.ToString() ?? string.Empty;
            }

            if (values.TryGetValue(nameof(NewGold), out var newGold))
            {
                NewGold = newGold?.ToString() ?? string.Empty;
            }

            if (values.TryGetValue(nameof(NewCost), out var newCost))
            {
                NewCost = newCost?.ToString() ?? string.Empty;
            }

            if (values.TryGetValue(nameof(NewDescription), out var newDescription))
            {
                NewDescription = newDescription?.ToString() ?? string.Empty;
            }

            if (values.TryGetValue(nameof(RarityMode), out var rarityMode) && rarityMode != null)
            {
                if (Enum.TryParse<FishingModifyRarityMode>(rarityMode.ToString(), true, out var parsedRarityMode))
                {
                    RarityMode = parsedRarityMode;
                }
            }

            if (values.TryGetValue(nameof(ManualRarity), out var manualRarity) && manualRarity != null)
            {
                if (Enum.TryParse<FishRarity>(manualRarity.ToString(), true, out var parsedRarity))
                {
                    ManualRarity = parsedRarity;
                }
            }

            if (values.TryGetValue(nameof(EnabledState), out var enabledState) && enabledState != null)
            {
                if (Enum.TryParse<ItemEnabledStateAction>(enabledState.ToString(), true, out var parsedEnabledState))
                {
                    EnabledState = parsedEnabledState;
                }
            }

            if (values.TryGetValue(nameof(NewBoostType), out var newBoostType) && newBoostType != null)
            {
                var boostTypeStr = newBoostType.ToString();
                if (string.IsNullOrWhiteSpace(boostTypeStr))
                {
                    NewBoostType = null;
                }
                else if (Enum.TryParse<FishingBoostType>(boostTypeStr, true, out var parsedBoostType))
                {
                    NewBoostType = parsedBoostType;
                }
            }

            if (values.TryGetValue(nameof(NewBoostAmount), out var newBoostAmount))
            {
                NewBoostAmount = newBoostAmount?.ToString() ?? string.Empty;
            }

            if (values.TryGetValue(nameof(NewTargetFish), out var newTargetFish))
            {
                NewTargetFish = newTargetFish?.ToString() ?? string.Empty;
            }

            if (values.TryGetValue(nameof(NewTargetCategory), out var newTargetCategory))
            {
                NewTargetCategory = newTargetCategory?.ToString() ?? string.Empty;
            }

            if (values.TryGetValue(nameof(NewEquipmentSlot), out var newEquipmentSlot))
            {
                NewEquipmentSlot = newEquipmentSlot?.ToString() ?? string.Empty;
            }

            if (values.TryGetValue(nameof(NewMaxUses), out var newMaxUses))
            {
                NewMaxUses = newMaxUses?.ToString() ?? string.Empty;
            }

            if (values.TryGetValue(nameof(AdminOnlyState), out var adminOnlyState) && adminOnlyState != null)
            {
                if (Enum.TryParse<ItemEnabledStateAction>(adminOnlyState.ToString(), true, out var parsedAdminOnlyState))
                {
                    AdminOnlyState = parsedAdminOnlyState;
                }
            }

            if (values.TryGetValue(nameof(Enabled), out var enabled) && bool.TryParse(enabled?.ToString(), out var parsedEnabled))
            {
                Enabled = parsedEnabled;
            }
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            var targetType = TargetType;
            if (values.TryGetValue(nameof(TargetType), out var targetTypeValue) && targetTypeValue != null)
            {
                Enum.TryParse<FishingModifyTargetType>(targetTypeValue.ToString(), true, out targetType);
            }

            if (targetType == FishingModifyTargetType.Fish)
            {
                if (!values.TryGetValue(nameof(TargetFish), out var fishValue) ||
                    string.IsNullOrWhiteSpace(fishValue?.ToString()))
                {
                    return "Fish to Modify is required";
                }

                if (values.TryGetValue(nameof(NewGold), out var goldValue) &&
                    !string.IsNullOrWhiteSpace(goldValue?.ToString()))
                {
                    var goldStr = goldValue.ToString()!.Trim();
                    if (!goldStr.Contains('%') && (!int.TryParse(goldStr, out var parsedGold) || parsedGold < 0))
                    {
                        return "New Base Gold must be a valid non-negative number or variable";
                    }
                }
            }
            else
            {
                if (!values.TryGetValue(nameof(TargetShopItem), out var shopItemValue) ||
                    string.IsNullOrWhiteSpace(shopItemValue?.ToString()))
                {
                    return "Shop Item to Modify is required";
                }

                if (values.TryGetValue(nameof(NewCost), out var costValue) &&
                    !string.IsNullOrWhiteSpace(costValue?.ToString()))
                {
                    var costStr = costValue.ToString()!.Trim();
                    if (!costStr.Contains('%') && (!int.TryParse(costStr, out var parsedCost) || parsedCost < 0))
                    {
                        return "New Cost must be a valid non-negative number or variable";
                    }
                }

                if (values.TryGetValue(nameof(NewBoostAmount), out var boostAmountValue) &&
                    !string.IsNullOrWhiteSpace(boostAmountValue?.ToString()))
                {
                    var boostStr = boostAmountValue.ToString()!.Trim();
                    if (!boostStr.Contains('%') && !double.TryParse(boostStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _) && !double.TryParse(boostStr, out _))
                    {
                        return "New Boost Amount must be a valid number or variable";
                    }
                }
            }

            return null;
        }
    }
}
