namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

public static class StarshipStarterEvents
{
    public static IReadOnlyList<string> FileNames { get; } = ["courier-relay.json", "survey-cache.json", "debris-crossing.json"];
    public static bool Install(StarshipExpeditionSettings settings, string webRootPath)
    {
        if (settings.StarterEventsInstalled) return false;
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
        settings.StarterEventsInstalled = true;
        StarshipExpeditionSettings.NormalizeAndValidate(settings);
        return true;
    }

}