using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class SubActionsRepository : GenericRepository<SubActionType>, ISubActionsRepository
    {
        public SubActionsRepository(ApplicationDbContext context) : base(context)
        {
        }

        public Task<Guid> GetNextIdAsync()
        {
            return Task.FromResult(Guid.CreateVersion7());
        }
    }

    // Custom JsonTypeInfoResolver for SubActionType polymorphism
    // Automatically discovers all SubActionType derived classes via reflection
    public class SubActionTypeResolver : DefaultJsonTypeInfoResolver
    {
        private static readonly Lazy<IReadOnlyList<JsonDerivedType>> _derivedTypes = new(() =>
        {
            var baseType = typeof(SubActionType);
            var assembly = baseType.Assembly;

            return assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && baseType.IsAssignableFrom(t) && t != baseType)
                .OrderBy(t => t.Name) // Consistent ordering
                .Select(t =>
                {
                    // Extract discriminator name from type name (e.g., "AlertType" -> "Alert")
                    var discriminator = t.Name.EndsWith("Type")
                        ? t.Name[..^4] // Remove "Type" suffix
                        : t.Name;
                    return new JsonDerivedType(t, discriminator);
                })
                .ToList();
        }, LazyThreadSafetyMode.ExecutionAndPublication);

        public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            JsonTypeInfo jsonTypeInfo = base.GetTypeInfo(type, options);

            Type baseType = typeof(SubActionType);
            if (jsonTypeInfo.Type == baseType)
            {
                // Create a new JsonPolymorphismOptions for each JsonTypeInfo instance
                jsonTypeInfo.PolymorphismOptions = new JsonPolymorphismOptions
                {
                    TypeDiscriminatorPropertyName = "$type",
                    IgnoreUnrecognizedTypeDiscriminators = true,
                    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization
                };

                // Add all discovered derived types
                foreach (var derivedType in _derivedTypes.Value)
                {
                    jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(derivedType);
                }
            }

            return jsonTypeInfo;
        }
    }
}
