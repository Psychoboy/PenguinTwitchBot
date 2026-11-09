namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

public static class StarshipStarterEvents
{
    private static readonly IReadOnlyList<string> LegacyFileNames =
        ["courier-relay.json", "survey-cache.json", "debris-crossing.json"];

    public static IReadOnlyList<string> FileNames { get; } =
    [
        "courier-relay.json",
        "survey-cache.json",
        "debris-crossing.json",
        "uncharted-wormhole.json",
        "ancient-shipyard.json",
        "ghost-convoy.json"
    ];

    public static bool Install(StarshipExpeditionSettings settings, string webRootPath)
    {
        settings.InstalledStarterEvents ??= [];
        if (settings.StarterEventsInstalled && settings.InstalledStarterEvents.Count == 0)
            settings.InstalledStarterEvents.AddRange(LegacyFileNames);

        var pendingFiles = FileNames
            .Where(file => !settings.InstalledStarterEvents.Contains(file, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var updatedExisting = false;
        foreach (var fileName in FileNames.Except(pendingFiles, StringComparer.OrdinalIgnoreCase))
        {
            var filePath = Path.Combine(webRootPath, "help", "starship", "examples", fileName);
            if (!File.Exists(filePath)) continue;
            StarshipEventDefinition bundled;
            try
            {
                bundled = StarshipEventDocument.Read(File.ReadAllText(filePath));
            }
            catch (InvalidDataException)
            {
                continue;
            }
            var existingIndex = settings.EventCatalog.FindIndex(definition => string.Equals(definition.EventType, bundled.EventType, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0 && settings.EventCatalog[existingIndex].CriticalSuccessUpgrade == null && bundled.CriticalSuccessUpgrade != null)
            {
                settings.EventCatalog[existingIndex] = settings.EventCatalog[existingIndex] with
                {
                    CriticalSuccessUpgrade = bundled.CriticalSuccessUpgrade
                };
                updatedExisting = true;
            }
        }
        if (pendingFiles.Count == 0)
        {
            if (updatedExisting) StarshipExpeditionSettings.NormalizeAndValidate(settings);
            return updatedExisting;
        }

        var nextOrder = settings.EventCatalog.Count == 0 ? 0 : settings.EventCatalog.Max(definition => definition.SortOrder) + 1;
        foreach (var fileName in pendingFiles)
        {
            settings.InstalledStarterEvents.Add(fileName);
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