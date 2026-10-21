namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

public static class StarshipStarterEvents
{
    public const int CurrentVersion = 1;
    public static IReadOnlyList<string> FileNames { get; } = ["courier-relay.json", "survey-cache.json", "debris-crossing.json"];
    public static bool Install(StarshipExpeditionSettings settings, string webRootPath)
    {
        if (settings.StarterEventsVersion == CurrentVersion) return false;
        if (settings.StarterEventsVersion != 0) throw new InvalidDataException("Unsupported Starship starter event version.");
        var nextOrder = settings.EventCatalog.Count == 0 ? 0 : settings.EventCatalog.Max(definition => definition.SortOrder) + 1;
        foreach (var fileName in FileNames)
        {
            var json = File.ReadAllText(Path.Combine(webRootPath, "help", "starship", "examples", fileName));
            StarshipEventDefinition starter;
            try
            {
                starter = StarshipEventDocument.Read(json);
            }
            catch (InvalidDataException ex)
            {
                throw new InvalidDataException($"Invalid bundled Starship starter event {fileName}.", ex);
            }
            if (settings.EventCatalog.Any(definition => string.Equals(definition.EventType, starter.EventType, StringComparison.OrdinalIgnoreCase))) continue;
            var candidate = starter with { SortOrder = nextOrder };
            settings.EventCatalog.Add(candidate);
            try
            {
                StarshipEventCatalog.Validate(settings.EventCatalog, settings);
                nextOrder++;
            }
            catch (ArgumentException)
            {
                settings.EventCatalog.Remove(candidate);
            }
        }
        settings.StarterEventsVersion = CurrentVersion;
        StarshipExpeditionSettings.NormalizeAndValidate(settings);
        return true;
    }

}