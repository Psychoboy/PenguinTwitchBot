using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using System.Reflection;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions
{
    /// <summary>
    /// Central registry that automatically discovers all SubAction types and their metadata.
    /// This eliminates manual registration in multiple places.
    /// </summary>
    public static class SubActionRegistry
    {
        private static readonly Lazy<Dictionary<SubActionTypes, SubActionMetadata>> _metadata = new(DiscoverSubActions);
        private static readonly Lazy<Dictionary<SubActionTypes, Type>> _types = new(DiscoverSubActionTypeMapping);

        public static IReadOnlyDictionary<SubActionTypes, SubActionMetadata> Metadata => _metadata.Value;
        public static IReadOnlyDictionary<SubActionTypes, Type> Types => _types.Value;

        private static Dictionary<SubActionTypes, SubActionMetadata> DiscoverSubActions()
        {
            var metadata = new Dictionary<SubActionTypes, SubActionMetadata>();

            var assembly = typeof(SubActionType).Assembly;
            var subActionTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(SubActionType)))
                .ToList();

            foreach (var type in subActionTypes)
            {
                var attribute = type.GetCustomAttribute<SubActionMetadataAttribute>();
                if (attribute == null)
                {
                    // Skip types without metadata attribute
                    continue;
                }

                // Try to get SubActionTypes enum value from the type's default instance or property
                var enumValue = GetSubActionTypeEnum(type);
                if (enumValue == SubActionTypes.None)
                {
                    continue;
                }

                var category = attribute.Category;
                if (string.IsNullOrEmpty(category) || category == SubActionCategories.General)
                {
                    category = InferCategory(attribute.DisplayName, enumValue);
                }

                metadata[enumValue] = new SubActionMetadata
                {
                    EnumValue = enumValue,
                    Type = type,
                    DisplayName = attribute.DisplayName,
                    Description = attribute.Description,
                    Icon = attribute.Icon,
                    Color = attribute.Color,
                    TableName = attribute.TableName,
                    Category = category
                };
            }

            return metadata;
        }

        private static Dictionary<SubActionTypes, Type> DiscoverSubActionTypeMapping()
        {
            var assembly = typeof(SubActionType).Assembly;
            return assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(SubActionType)))
                .Where(t => t.GetCustomAttribute<SubActionMetadataAttribute>() != null)
                .ToDictionary(
                    t => GetSubActionTypeEnum(t),
                    t => t
                )
                .Where(kvp => kvp.Key != SubActionTypes.None)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        private static SubActionTypes GetSubActionTypeEnum(Type type)
        {
            // Try to instantiate and get the SubActionTypes property
            try
            {
                var instance = Activator.CreateInstance(type) as SubActionType;
                return instance?.SubActionTypes ?? SubActionTypes.None;
            }
            catch
            {
                // If instantiation fails, try to infer from type name
                var typeName = type.Name.Replace("Type", "");
                if (Enum.TryParse<SubActionTypes>(typeName, out var enumValue))
                {
                    return enumValue;
                }
                return SubActionTypes.None;
            }
        }

        public static SubActionMetadata? GetMetadata(SubActionTypes type)
        {
            return Metadata.TryGetValue(type, out var metadata) ? metadata : null;
        }

        public static Type? GetSubActionType(SubActionTypes type)
        {
            return Types.TryGetValue(type, out var subActionType) ? subActionType : null;
        }

        private static string InferCategory(string displayName, SubActionTypes type)
        {
            if (displayName.StartsWith("OBS -", StringComparison.OrdinalIgnoreCase) || type.ToString().StartsWith("Obs"))
                return SubActionCategories.Obs;
            if (displayName.StartsWith("Fishing", StringComparison.OrdinalIgnoreCase) || type.ToString().StartsWith("Fishing"))
                return SubActionCategories.Fishing;
            if (displayName.StartsWith("Raffle", StringComparison.OrdinalIgnoreCase) || type.ToString().StartsWith("Raffle"))
                return SubActionCategories.Raffles;
            if (displayName.StartsWith("Overlay Timer", StringComparison.OrdinalIgnoreCase) || type.ToString().StartsWith("OverlayTimer"))
                return SubActionCategories.OverlayTimer;

            return type switch
            {
                SubActionTypes.CheckPoints or SubActionTypes.GiftPoints or SubActionTypes.ExecutePointCommand
                    or SubActionTypes.ChannelPointSetEnabledState or SubActionTypes.ChannelPointSetPausedState
                    => SubActionCategories.PointsAndRewards,

                SubActionTypes.SendMessage or SubActionTypes.ReplyToMessage or SubActionTypes.Tts
                    or SubActionTypes.Alert or SubActionTypes.PlaySound
                    => SubActionCategories.ChatAndMedia,

                SubActionTypes.LogicIfElse or SubActionTypes.Break or SubActionTypes.Delay
                    or SubActionTypes.ExecuteAction or SubActionTypes.ExecuteDefaultCommand
                    or SubActionTypes.ToggleCommandDisabledState or SubActionTypes.TimerGroupSetEnabledState
                    or SubActionTypes.ResetCooldowns
                    => SubActionCategories.LogicAndFlow,

                SubActionTypes.Followage or SubActionTypes.Uptime or SubActionTypes.WatchTime
                    or SubActionTypes.ForEachViewer or SubActionTypes.SelectRandomViewers
                    or SubActionTypes.GiveawayPrize
                    => SubActionCategories.TwitchAndViewers,

                SubActionTypes.SetVariable or SubActionTypes.SetGlobalVariable or SubActionTypes.GetGlobalVariable
                    or SubActionTypes.MultiCounter or SubActionTypes.RandomInt or SubActionTypes.CurrentTime
                    or SubActionTypes.ExternalApi or SubActionTypes.WriteFile
                    => SubActionCategories.VariablesAndUtilities,

                _ => SubActionCategories.General
            };
        }
    }

    public class SubActionMetadata
    {
        public SubActionTypes EnumValue { get; init; }
        public Type Type { get; init; } = null!;
        public string DisplayName { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Icon { get; init; } = string.Empty;
        public string Color { get; init; } = string.Empty;
        public string TableName { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
    }
}
