using PenguinTwitchBot.Bot.Commands.Games;

namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

public sealed class StarshipExpeditionSettings : IGameSessionMessageSettings
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumLegCount = 24;
    public const int MaximumEventIntervalLimitMinutes = 120;
    public const int MaximumCustomSectors = 20;
    public const int MaximumSectorNameLength = 40;
    public const long MaximumConfiguredReward = 100_000;
    public const long MaximumUnbankedReward = 1_000_000;

    public static IReadOnlyList<string> BuiltInSectors { get; } =
    [
        "Kepler Reach",
        "Ternion Belt",
        "Vela Expanse",
        "Asterion Drift",
        "Pioneer Verge"
    ];

    public static IReadOnlyList<string> RewardEventTypes { get; } =
    ["RouteChoice", "SignalDetected", "SalvageSite", "Hazard"];

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public int CatalogStorageVersion { get; set; }
    public int StarterEventsVersion { get; set; }
    public int LegCount { get; set; } = StarshipExpeditionService.DefaultLegCount;
    public int MinimumEventIntervalMinutes { get; set; } = StarshipExpeditionService.DefaultMinimumEventIntervalMinutes;
    public int MaximumEventIntervalMinutes { get; set; } = StarshipExpeditionService.DefaultMaximumEventIntervalMinutes;
    public int StationIntervalLegs { get; set; } = StarshipExpeditionService.DefaultStationIntervalLegs;
    public int EventVoteDurationSeconds { get; set; } = StarshipExpeditionService.DefaultVoteDurationSeconds;
    public int BoardingDurationSeconds { get; set; } = StarshipExpeditionService.DefaultBoardingDurationSeconds;
    public bool ActiveCrewRewardsOnly { get; set; } = true;
    public List<string> DisabledBuiltInSectors { get; set; } = [];
    public List<string> CustomSectors { get; set; } = [];
    public List<StarshipRewardProfile> RewardProfiles { get; set; } = CreateDefaultRewardProfiles();
    public Dictionary<string, string> EventRewardProfileIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<StarshipEventDefinition> EventCatalog { get; set; } = StarshipEventCatalog.CreateDefaults();
    public bool AnnounceSessionStatusInChat { get; set; } = true;
    public string SessionStartChatMessageTemplate { get; set; } = "Starship Expedition boarding is OPEN for {duration}s! Type !starship join before launch!";
    public string SessionEndChatMessageTemplate { get; set; } = "Starship Expedition {result}. {count} crew members were aboard for {duration}s.";
    public StarshipAppearanceSettings Appearance { get; set; } = new();
    public StarshipShipSystemsSettings ShipSystems { get; set; } = new();

    public static StarshipExpeditionSettings NormalizeAndValidate(StarshipExpeditionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException($"Unsupported Starship settings version {settings.SchemaVersion}.");
        if (settings.CatalogStorageVersion is < 0 or > 1)
            throw new InvalidDataException($"Unsupported Starship catalog storage version {settings.CatalogStorageVersion}.");
        if (settings.StarterEventsVersion is < 0 or > StarshipStarterEvents.CurrentVersion)
            throw new InvalidDataException($"Unsupported Starship starter event version {settings.StarterEventsVersion}.");
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
        settings.Appearance = StarshipAppearanceSettings.NormalizeAndValidate(settings.Appearance);
        settings.ShipSystems ??= new();
        settings.ShipSystems.Validate();
        settings.CustomSectors ??= [];
        settings.DisabledBuiltInSectors ??= [];
        if (settings.DisabledBuiltInSectors.Any(name => name == null || !BuiltInSectors.Contains(name.Trim(), StringComparer.OrdinalIgnoreCase)))
            throw new ArgumentException("Only known built-in sectors can be disabled.", nameof(settings.DisabledBuiltInSectors));
        settings.DisabledBuiltInSectors = BuiltInSectors
            .Where(name => settings.DisabledBuiltInSectors.Any(disabled => string.Equals(disabled.Trim(), name, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        settings.RewardProfiles ??= CreateDefaultRewardProfiles();
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
        if (settings.GetEffectiveSectors().Count < 2)
            throw new ArgumentException("Enable or add at least two expedition sectors.", nameof(settings.DisabledBuiltInSectors));

        foreach (var eventType in RewardEventTypes)
        {
            if (!settings.RewardProfiles.Any(profile => string.Equals(profile.EventType, eventType, StringComparison.OrdinalIgnoreCase)))
                settings.RewardProfiles.Add(new StarshipRewardProfile { EventType = eventType });
        }
        var configuredEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var profileIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in settings.RewardProfiles)
        {
            profile.EventType = (profile.EventType ?? string.Empty).Trim();
            if (profile.EventType.Length > 0 && !RewardEventTypes.Contains(profile.EventType, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException($"'{profile.EventType}' is not a rewardable built-in event.", nameof(settings.RewardProfiles));
            if (profile.EventType.Length > 0 && !configuredEvents.Add(profile.EventType))
                throw new ArgumentException($"A reward profile already exists for {profile.EventType}.", nameof(settings.RewardProfiles));
            if (profile.SuccessChancePercent is < 0 or > 100)
                throw new ArgumentOutOfRangeException(nameof(profile.SuccessChancePercent), "Success chance must be between 0 and 100 percent.");
            if (profile.UsesLegacyFields)
            {
                if (profile.Enabled && profile.PointTypeId <= 0)
                    throw new ArgumentOutOfRangeException(nameof(profile.PointTypeId), $"Select a point type for {profile.EventType}.");
                if (profile.CommonWeightPercent is < 0 or > 100 || profile.UncommonWeightPercent is < 0 or > 100 || profile.RareWeightPercent is < 0 or > 100)
                    throw new ArgumentOutOfRangeException(nameof(profile.CommonWeightPercent), "Rarity weights must be between 0 and 100 percent.");
                if (profile.Enabled && profile.CommonWeightPercent + profile.UncommonWeightPercent + profile.RareWeightPercent != 100)
                    throw new ArgumentException($"Rarity weights for {profile.EventType} must total 100 percent.", nameof(settings.RewardProfiles));
                ValidateRewardRange(profile.CommonMinimum, profile.CommonMaximum, nameof(profile.CommonMinimum));
                ValidateRewardRange(profile.UncommonMinimum, profile.UncommonMaximum, nameof(profile.UncommonMinimum));
                ValidateRewardRange(profile.RareMinimum, profile.RareMaximum, nameof(profile.RareMinimum));
                if (profile.MaximumPerEvent is < 0 or > MaximumConfiguredReward ||
                    profile.MaximumUnbankedPerVoyage is < 0 or > MaximumUnbankedReward)
                    throw new ArgumentOutOfRangeException(nameof(profile.MaximumPerEvent), "Point limits must be non-negative or unlimited.");
                if (profile.RiskLossPercent is < 0 or > 100 || profile.MaximumRiskLossPerEvent is < 0 or > MaximumConfiguredReward)
                    throw new ArgumentOutOfRangeException(nameof(profile.RiskLossPercent), "Risk loss must be 0-100 percent with a non-negative amount cap.");
            }

            profile.PointTypeName = profile.PointTypeName?.Trim() ?? string.Empty;
            profile.ConvertLegacyToTiers();
            profile.ValidateTiers(profile.EventType);
            profile.ValidateCapsAndLoss(profile.EventType);
            profile.PointTypeId = profile.GetPrimaryPointTypeId();
            profile.Id = string.IsNullOrWhiteSpace(profile.Id) ? profile.EventType : profile.Id.Trim();
            profile.Name = string.IsNullOrWhiteSpace(profile.Name) ? profile.EventType : profile.Name.Trim();
            if (profile.Id.Length is < 1 or > 80 || !profileIds.Add(profile.Id))
                throw new ArgumentException("Reward profile IDs must be unique and contain 1-80 characters.", nameof(settings.RewardProfiles));
            if (profile.Name.Length is < 1 or > 80)
                throw new ArgumentException("Reward profile names must contain 1-80 characters.", nameof(settings.RewardProfiles));
        }

        settings.EventRewardProfileIds = new Dictionary<string, string>(settings.EventRewardProfileIds ?? [], StringComparer.OrdinalIgnoreCase);
        foreach (var eventType in RewardEventTypes)
        {
            if (!settings.EventRewardProfileIds.ContainsKey(eventType))
                settings.EventRewardProfileIds[eventType] = settings.RewardProfiles.First(profile => profile.EventType.Equals(eventType, StringComparison.OrdinalIgnoreCase)).Id;
        }
        foreach (var binding in settings.EventRewardProfileIds)
        {
            if (!RewardEventTypes.Contains(binding.Key, StringComparer.OrdinalIgnoreCase) ||
                binding.Value.Length > 0 && !profileIds.Contains(binding.Value))
                throw new ArgumentException("An event reward binding references an unknown event or profile.", nameof(settings.EventRewardProfileIds));
        }

        StarshipEventCatalog.Validate(settings.EventCatalog, settings);
        return settings;
    }

    public List<string> GetEffectiveSectors() =>
        [.. BuiltInSectors.Where(name => !DisabledBuiltInSectors.Contains(name, StringComparer.OrdinalIgnoreCase)), .. CustomSectors];

    public StarshipRewardProfile? GetRewardProfile(string eventType) =>
        StarshipRewardProfile.FindForEvent(RewardProfiles, GetEffectiveRewardBindings(), eventType);

    public Dictionary<string, string> GetEffectiveRewardBindings()
    {
        var bindings = new Dictionary<string, string>(EventRewardProfileIds, StringComparer.OrdinalIgnoreCase);
        foreach (var definition in EventCatalog.Where(definition => definition.RewardProfileId != null))
            bindings[definition.EventType] = definition.RewardProfileId!;
        return bindings;
    }

    private static List<StarshipRewardProfile> CreateDefaultRewardProfiles() => RewardEventTypes
        .Select(eventType => new StarshipRewardProfile { EventType = eventType })
        .ToList();

    private static void ValidateRewardRange(long minimum, long maximum, string propertyName)
    {
        if (minimum < 0 || maximum < minimum || maximum > MaximumConfiguredReward)
            throw new ArgumentOutOfRangeException(propertyName, "Reward ranges must be non-negative and the maximum must be at least the minimum.");
    }
}

public sealed partial class StarshipRewardProfile
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<StarshipRewardTier> Tiers { get; set; } = [];
    public List<StarshipRewardCap> Caps { get; set; } = [];
    public List<StarshipLossRule> LossRules { get; set; } = [];
    public List<StarshipRewardAction> FailureActions { get; set; } = [];
    public string EventType { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int PointTypeId { get; set; }
    public string PointTypeName { get; set; } = string.Empty;
    public int SuccessChancePercent { get; set; } = 80;
    public int CommonWeightPercent { get; set; } = 70;
    public int UncommonWeightPercent { get; set; } = 25;
    public int RareWeightPercent { get; set; } = 5;
    public long CommonMinimum { get; set; } = 5;
    public long CommonMaximum { get; set; } = 10;
    public long UncommonMinimum { get; set; } = 12;
    public long UncommonMaximum { get; set; } = 25;
    public long RareMinimum { get; set; } = 30;
    public long RareMaximum { get; set; } = 50;
    public long? MaximumPerEvent { get; set; }
    public long? MaximumUnbankedPerVoyage { get; set; }
    public int RiskLossPercent { get; set; } = 25;
    public long? MaximumRiskLossPerEvent { get; set; }
}
