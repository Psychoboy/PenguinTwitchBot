using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using System.Reflection;

namespace DotNetTwitchBot.Bot.Actions.SubActions
{
    /// <summary>
    /// Central registry that automatically discovers all SubAction types and their metadata.
    /// This eliminates manual registration in multiple places.
    /// </summary>
    public static class SubActionRegistry
    {
        private static readonly Lazy<Dictionary<SubActionTypes, SubActionMetadata>> _metadata = new(DiscoverSubActions);
        private static readonly Lazy<Dictionary<SubActionTypes, Type>> _types = new(DiscoverSubActionTypeMapping);
        private static readonly Lazy<Dictionary<SubActionTypes, Type>> _handlers = new(DiscoverHandlers);

        public static IReadOnlyDictionary<SubActionTypes, SubActionMetadata> Metadata => _metadata.Value;
        public static IReadOnlyDictionary<SubActionTypes, Type> Types => _types.Value;
        public static IReadOnlyDictionary<SubActionTypes, Type> Handlers => _handlers.Value;

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

                metadata[enumValue] = new SubActionMetadata
                {
                    EnumValue = enumValue,
                    Type = type,
                    DisplayName = attribute.DisplayName,
                    Description = attribute.Description,
                    Icon = attribute.Icon,
                    Color = attribute.Color,
                    TableName = attribute.TableName
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

        private static Dictionary<SubActionTypes, Type> DiscoverHandlers()
        {
            var assembly = typeof(ISubActionHandler).Assembly;
            var handlerTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(ISubActionHandler).IsAssignableFrom(t))
                .ToList();

            var handlers = new Dictionary<SubActionTypes, Type>();

            foreach (var handlerType in handlerTypes)
            {
                // Try to get the SupportedType by looking at the interface implementation
                // This requires instantiating or using reflection to find the property
                var supportedTypeProperty = handlerType.GetProperty("SupportedType");
                if (supportedTypeProperty != null)
                {
                    // For now, we'll register handlers by convention (HandlerName -> SubActionType)
                    // A more robust approach would require instantiating handlers
                    var handlerName = handlerType.Name.Replace("Handler", "");
                    if (Enum.TryParse<SubActionTypes>(handlerName, out var enumValue))
                    {
                        handlers[enumValue] = handlerType;
                    }
                }
            }

            return handlers;
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
    }
}
