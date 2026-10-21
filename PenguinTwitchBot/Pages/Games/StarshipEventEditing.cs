using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using PenguinTwitchBot.Bot.Commands.Games.Starship;

namespace PenguinTwitchBot.Pages.Games;

public static class StarshipEventEditing
{
    public const int MaximumImportBytes = 256 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
        MaxDepth = 32,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true
    };

    public static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)!;
    public static T Convert<T>(object value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)!;

    public static StarshipEventDefinition NewEvent(StarshipExpeditionSettings settings) => new(
        UniqueId("custom-event", settings), "New Event", "A discovery near {subject}.",
        [new() { ChoiceId = "investigate", Label = "Investigate" }, new() { ChoiceId = "pass", Label = "Continue on course", EndsWithoutReward = true }],
        "pass", "The crew recovers {loot}.", "The crew returns empty-handed.")
        { SortOrder = settings.EventCatalog.Count, SubjectStyle = "signal", ObjectKind = "beacon", RewardProfileId = "" };

    public static StarshipEventDefinition Duplicate(StarshipEventDefinition source, StarshipExpeditionSettings settings)
    {
        var copy = Copy(source);
        var slug = Regex.Replace(source.EventType.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        if (slug.Length == 0 || !char.IsAsciiLetterLower(slug[0])) slug = "event";
        slug = slug[..Math.Min(60, slug.Length)].TrimEnd('-');
        return copy with
        {
            EventType = UniqueId(slug + "-copy", settings),
            Title = source.Title[..Math.Min(94, source.Title.Length)] + " Copy",
            SortOrder = settings.EventCatalog.Count,
            RewardProfileId = source.RewardProfileId ?? settings.EventRewardProfileIds.GetValueOrDefault(source.EventType, "")
        };
    }

    private static string UniqueId(string basis, StarshipExpeditionSettings settings)
    {
        var id = basis;
        for (var suffix = 2; settings.EventCatalog.Any(definition => string.Equals(definition.EventType, id, StringComparison.OrdinalIgnoreCase)); suffix++)
            id = $"{basis}-{suffix}";
        return id;
    }

    public static void ValidateDraft(StarshipExpeditionSettings settings, StarshipEventDefinition draft,
        string? originalId, StarshipRewardProfile? ownedProfile = null)
    {
        var candidate = Copy(settings);
        var index = originalId == null ? -1 : candidate.EventCatalog.FindIndex(definition => definition.EventType == originalId);
        if (originalId != null && index < 0) throw new ArgumentException("The event being edited no longer exists.");
        if (originalId != null && draft.EventType != originalId) throw new ArgumentException("Existing event IDs cannot be changed.");
        if (ownedProfile != null)
        {
            if (candidate.RewardProfiles.Any(profile => profile.Id == ownedProfile.Id)) throw new ArgumentException("The new reward profile ID already exists.");
            candidate.RewardProfiles.Add(Copy(ownedProfile));
        }
        if (index < 0) candidate.EventCatalog.Add(Copy(draft));
        else candidate.EventCatalog[index] = Copy(draft);
        StarshipExpeditionSettings.NormalizeAndValidate(candidate);
    }

    public static string Export(StarshipEventDefinition definition) => JsonSerializer.Serialize(new EventDocument(1, definition), JsonOptions);

    public static StarshipEventDefinition Import(string json, StarshipExpeditionSettings settings)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaximumImportBytes) throw new ArgumentException("Event JSON exceeds 256 KB.");
        try
        {
            var document = JsonSerializer.Deserialize<EventDocument>(json, JsonOptions);
            if (document == null || document.SchemaVersion != 1 || document.Event == null)
                throw new ArgumentException("Use a supported single-event JSON document (schemaVersion 1).");
            ValidateShape(document.Event);
            var imported = Duplicate(document.Event, settings);
            var validationSettings = Copy(settings);
            if (!string.IsNullOrEmpty(imported.RewardProfileId) && !validationSettings.RewardProfiles.Any(profile => profile.Id == imported.RewardProfileId))
                validationSettings.RewardProfiles.Add(new StarshipRewardProfile { Id = imported.RewardProfileId, Name = "Imported profile reference" });
            ValidateDraft(validationSettings, imported, null);
            return imported;
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("Event JSON is malformed or contains unsupported fields.", ex);
        }
    }

    private static void ValidateShape(StarshipEventDefinition definition)
    {
        if (definition.Choices == null || definition.Choices.Any(choice => choice == null) ||
            definition.SubjectNames == null || definition.Images == null || definition.LootByTier == null ||
            definition.AllowedRoutes == null || definition.AllowedSectors == null)
            throw new ArgumentException("Event lists and choices cannot be null.");
    }

    private sealed record EventDocument(int SchemaVersion, StarshipEventDefinition Event);
}