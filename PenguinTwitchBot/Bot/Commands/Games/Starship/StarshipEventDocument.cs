using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

public sealed record StarshipCatalogExportDocument(int SchemaVersion, List<StarshipEventDefinition> Events)
{
    public string? AiPromptGuide { get; init; }
    public List<string>? AvailableRoutes { get; init; }
    public List<string>? AvailableSectors { get; init; }
    public List<string>? RewardProfileIds { get; init; }
    public List<string>? StoryTokens { get; init; }
    public List<string>? SupportedOutcomes { get; init; }
}

public sealed record StarshipEventDocument(int SchemaVersion, StarshipEventDefinition Event)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        MaxDepth = 32,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        WriteIndented = true
    };

    public static StarshipEventDefinition Read(string json)
    {
        var document = JsonSerializer.Deserialize<StarshipEventDocument>(json, JsonOptions);
        if (document == null || document.SchemaVersion != 2 || document.Event == null)
            throw new InvalidDataException("Use a supported branching event JSON document (schemaVersion 2).");
        return document.Event;
    }

    public static IReadOnlyList<StarshipEventDefinition> ReadMany(string json)
    {
        var root = JsonNode.Parse(json) as JsonObject
            ?? throw new InvalidDataException("Use a supported Starship event JSON document (schemaVersion 2).");
        if (root.ContainsKey("events"))
        {
            var catalogDoc = JsonSerializer.Deserialize<StarshipCatalogExportDocument>(json, JsonOptions);
            if (catalogDoc == null || catalogDoc.SchemaVersion != 2 || catalogDoc.Events == null || catalogDoc.Events.Count == 0)
                throw new InvalidDataException("Use a supported Starship event catalog JSON document (schemaVersion 2) with at least one event.");
            return catalogDoc.Events;
        }

        return [Read(json)];
    }

    public static string Write(StarshipEventDefinition definition) =>
        JsonSerializer.Serialize(new StarshipEventDocument(2, definition), JsonOptions);

    public static string WriteCatalog(StarshipExpeditionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var events = settings.EventCatalog
            .Where(definition => !StarshipEventCatalog.IsStructural(definition.EventType))
            .OrderBy(definition => definition.SortOrder)
            .ThenBy(definition => definition.EventType, StringComparer.Ordinal)
            .ToList();
        var export = new StarshipCatalogExportDocument(2, events)
        {
            AiPromptGuide = "Starship Expedition Event Catalog (schemaVersion 2). To generate new events for import, return a JSON object with \"schemaVersion\": 2 and an \"events\" array (or a single \"event\" object). Event IDs (eventType), stepId, and choiceId must be lowercase kebab-case slugs. Every step graph must start at startStepId, contain no cycles, have 2-4 choices per Vote step, and end at one or more End steps. End steps may optionally set fixedOutcome, rewardMultiplierOverridePercent, riskMultiplierOverridePercent, shipEffectOverride, grantedUpgrade, targetRoute, or completesVoyageEarly.",
            AvailableRoutes = settings.GetAllConfiguredRoutes(),
            AvailableSectors = settings.GetEffectiveSectors(),
            RewardProfileIds = settings.RewardProfiles.Select(profile => profile.Id).ToList(),
            StoryTokens = ["{crew}", "{crewlist}", "{subject}", "{Subject}", "{loot}", "{rarity}", "{sector}"],
            SupportedOutcomes = [.. StarshipExpeditionNarrative.AllOutcomes]
        };
        return JsonSerializer.Serialize(export, JsonOptions);
    }
}