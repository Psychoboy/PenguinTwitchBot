using PenguinTwitchBot.Bot.Commands.Games;

namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

public sealed class StarshipExpeditionSettings : IGameSessionMessageSettings
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumLegCount = 24;
    public const int MaximumEventIntervalLimitMinutes = 120;
    public const int MaximumCustomSectors = 20;
    public const int MaximumSectorNameLength = 40;

    public static IReadOnlyList<string> BuiltInSectors { get; } =
    [
        "Kepler Reach",
        "Ternion Belt",
        "Vela Expanse",
        "Asterion Drift",
        "Pioneer Verge"
    ];

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public int LegCount { get; set; } = StarshipExpeditionService.DefaultLegCount;
    public int MinimumEventIntervalMinutes { get; set; } = StarshipExpeditionService.DefaultMinimumEventIntervalMinutes;
    public int MaximumEventIntervalMinutes { get; set; } = StarshipExpeditionService.DefaultMaximumEventIntervalMinutes;
    public int StationIntervalLegs { get; set; } = StarshipExpeditionService.DefaultStationIntervalLegs;
    public int EventVoteDurationSeconds { get; set; } = StarshipExpeditionService.DefaultVoteDurationSeconds;
    public int BoardingDurationSeconds { get; set; } = StarshipExpeditionService.DefaultBoardingDurationSeconds;
    public bool ActiveCrewRewardsOnly { get; set; } = true;
    public List<string> CustomSectors { get; set; } = [];
    public bool AnnounceSessionStatusInChat { get; set; } = true;
    public string SessionStartChatMessageTemplate { get; set; } = "Starship Expedition boarding is OPEN for {duration}s! Type !starship join before launch!";
    public string SessionEndChatMessageTemplate { get; set; } = "Starship Expedition {result}. {count} crew members were aboard for {duration}s.";

    public static StarshipExpeditionSettings NormalizeAndValidate(StarshipExpeditionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException($"Unsupported Starship settings version {settings.SchemaVersion}.");
        if (settings.LegCount is < 1 or > MaximumLegCount)
            throw new ArgumentOutOfRangeException(nameof(settings.LegCount), $"Voyage legs must be between 1 and {MaximumLegCount}.");
        if (settings.MinimumEventIntervalMinutes is < 1 or > MaximumEventIntervalLimitMinutes)
            throw new ArgumentOutOfRangeException(nameof(settings.MinimumEventIntervalMinutes), $"Minimum event interval must be between 1 and {MaximumEventIntervalLimitMinutes} minutes.");
        if (settings.MaximumEventIntervalMinutes < settings.MinimumEventIntervalMinutes || settings.MaximumEventIntervalMinutes > MaximumEventIntervalLimitMinutes)
            throw new ArgumentOutOfRangeException(nameof(settings.MaximumEventIntervalMinutes), "Maximum event interval must be at least the minimum and no more than 120 minutes.");
        if (settings.StationIntervalLegs is < 1 or > MaximumLegCount)
            throw new ArgumentOutOfRangeException(nameof(settings.StationIntervalLegs), $"Station interval must be between 1 and {MaximumLegCount} legs.");
        if (settings.EventVoteDurationSeconds is < 10 or > 300)
            throw new ArgumentOutOfRangeException(nameof(settings.EventVoteDurationSeconds), "Event vote duration must be between 10 and 300 seconds.");
        if (settings.BoardingDurationSeconds is < 10 or > 300)
            throw new ArgumentOutOfRangeException(nameof(settings.BoardingDurationSeconds), "Crew boarding duration must be between 10 and 300 seconds.");
        settings.SessionStartChatMessageTemplate ??= string.Empty;
        settings.SessionEndChatMessageTemplate ??= string.Empty;
        settings.CustomSectors ??= [];
        if (settings.CustomSectors.Count > MaximumCustomSectors)
            throw new ArgumentOutOfRangeException(nameof(settings.CustomSectors), $"Add no more than {MaximumCustomSectors} custom sectors.");

        settings.CustomSectors = settings.CustomSectors
            .Select(name => name.Trim())
            .ToList();
        if (settings.CustomSectors.Any(name => string.IsNullOrWhiteSpace(name) || name.Length > MaximumSectorNameLength))
            throw new ArgumentException($"Custom sector names must contain 1-{MaximumSectorNameLength} characters.", nameof(settings.CustomSectors));

        var allNames = BuiltInSectors.Concat(settings.CustomSectors).ToList();
        if (allNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() != allNames.Count)
            throw new ArgumentException("Sector names must be unique, including built-in sectors.", nameof(settings.CustomSectors));

        return settings;
    }

    public List<string> GetEffectiveSectors() => [.. BuiltInSectors, .. CustomSectors];
}
