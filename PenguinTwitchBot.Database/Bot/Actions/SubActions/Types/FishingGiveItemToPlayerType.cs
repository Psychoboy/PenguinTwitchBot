using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;
using System.ComponentModel.DataAnnotations.Schema;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Fishing Give Item To Player",
        description: "Give a fishing item directly to a specific player",
        icon: "mdi-gift",
        color: "Primary",
        tableName: "subactions_fishinggiveitemtoplayer")]
    public class FishingGiveItemToPlayerType : SubActionType, ISubActionUIProvider
    {
        public FishingGiveItemToPlayerType()
        {
            SubActionTypes = SubActionTypes.FishingGiveItemToPlayer;
        }

        [Column("PlayerUsername")]
        public string TargetName { get; set; } = "%user%";

        // Keep this mapped for backward compatibility with existing saved rows.
        public string PlayerUserId { get; set; } = string.Empty;
        public int? ShopItemId { get; set; }
        public string ShopItemName { get; set; } = string.Empty;

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return
            [
                new()
                {
                    PropertyName = nameof(TargetName),
                    Label = "Target Username",
                    FieldType = UIFieldType.Text,
                    Required = true,
                    HelperText = "Who should receive the item. You can use variables like %target% or %user%."
                },
                new()
                {
                    PropertyName = nameof(ShopItemId),
                    Label = "Fishing Item",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    SelectOptions = []
                },
                new()
                {
                    PropertyName = nameof(Enabled),
                    Label = "Enabled",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Success"
                }
            ];
        }

        public Dictionary<string, object?> GetValues()
        {
            return new Dictionary<string, object?>
            {
                { nameof(TargetName), TargetName },
                { nameof(PlayerUserId), PlayerUserId },
                { nameof(ShopItemId), ShopItemId?.ToString() ?? string.Empty },
                { nameof(ShopItemName), ShopItemName },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(TargetName), out var targetName))
            {
                TargetName = targetName?.ToString() ?? string.Empty;
            }

            if (values.TryGetValue(nameof(ShopItemId), out var shopItemId)
                && int.TryParse(shopItemId?.ToString(), out var parsedShopItemId)
                && parsedShopItemId > 0)
            {
                ShopItemId = parsedShopItemId;
            }
            else
            {
                ShopItemId = null;
            }

            if (values.TryGetValue(nameof(ShopItemName), out var shopItemName))
            {
                ShopItemName = shopItemName?.ToString() ?? string.Empty;
            }

            if (values.TryGetValue(nameof(Enabled), out var enabled) && bool.TryParse(enabled?.ToString(), out var parsedEnabled))
            {
                Enabled = parsedEnabled;
            }
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(TargetName), out var targetName)
                || string.IsNullOrWhiteSpace(targetName?.ToString()))
            {
                return "Target Username is required";
            }

            if (!values.TryGetValue(nameof(ShopItemId), out var shopItemId)
                || !int.TryParse(shopItemId?.ToString(), out var parsedShopItemId)
                || parsedShopItemId <= 0)
            {
                return "Fishing Item is required";
            }

            return null;
        }
    }
}
