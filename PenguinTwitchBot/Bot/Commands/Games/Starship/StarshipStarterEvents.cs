using System.Text.Json;
using System.Text.Json.Serialization;

namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

public static class StarshipStarterEvents
{
    public const int CurrentVersion = 1;
    public static IReadOnlyList<string> FileNames { get; } = ["courier-relay.json", "survey-cache.json", "debris-crossing.json"];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true
    };

    public static bool Install(StarshipExpeditionSettings settings, string webRootPath)
    {
        if (settings.StarterEventsVersion == CurrentVersion) return false;
        if (settings.StarterEventsVersion != 0) throw new InvalidDataException("Unsupported Starship starter event version.");
        var nextOrder = settings.EventCatalog.Count == 0 ? 0 : settings.EventCatalog.Max(definition => definition.SortOrder) + 1;
        foreach (var fileName in FileNames)
        {
            var json = File.ReadAllText(Path.Combine(webRootPath, "help", "starship", "examples", fileName));
            var document = JsonSerializer.Deserialize<StarterDocument>(json, JsonOptions);
            if (document == null || document.SchemaVersion != 1 || document.Event == null)
                throw new InvalidDataException($"Invalid bundled Starship starter event {fileName}.");
            if (settings.EventCatalog.Any(definition => string.Equals(definition.EventType, document.Event.EventType, StringComparison.OrdinalIgnoreCase))) continue;
            settings.EventCatalog.Add(document.Event with { SortOrder = nextOrder++ });
        }
        settings.StarterEventsVersion = CurrentVersion;
        StarshipExpeditionSettings.NormalizeAndValidate(settings);
        return true;
    }

    private sealed record StarterDocument(int SchemaVersion, StarshipEventDefinition Event);
}