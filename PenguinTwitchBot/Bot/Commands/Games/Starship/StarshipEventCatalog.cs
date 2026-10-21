using System.Text.RegularExpressions;

namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

public static partial class StarshipEventCatalog
{
    public const int MaximumChoices = 6;
    public static IReadOnlyList<string> Routes { get; } = ["Uncharted", "Direct Route", "Survey Route"];
    private static readonly HashSet<string> Tokens = new(StringComparer.Ordinal)
        { "crew", "crewlist", "subject", "Subject", "loot", "rarity", "sector" };

    public static List<StarshipEventDefinition> CreateDefaults() =>
    [
        new("SignalDetected", "Unidentified Signal", "A signal from {subject} is repeating at the edge of sensor range. Scan it, trace its origin, then investigate - or move on.",
            [
                new() { ChoiceId = "scan", Label = "Scan the signal", ContinuesEvent = true, GrantsFlag = "scanned", RevealsRarity = true, Story = "{crew} decodes the looping message from {subject}: a plea in a long-dead dialect, guarding {loot}." },
                new() { ChoiceId = "trace", Label = "Trace its origin", ContinuesEvent = true, GrantsFlag = "traced", SuccessChanceModifierPercent = 15, RewardMultiplierPercent = 85, RiskMultiplierPercent = 60, Story = "{crewlist} triangulate {subject} to a hidden dock deep in the {sector}." },
                new() { ChoiceId = "investigate", Label = "Investigate the source", Story = "{crew} takes a shuttle out to {subject}." },
                new() { ChoiceId = "ignore", Label = "Continue on course", EndsWithoutReward = true, Story = "The crew lets {subject} fade into static." }
            ], "ignore", "It was real: the crew recovers {loot}.", "It was a decoy. The shuttle limps home empty and the crew loses cargo.")
        { SortOrder = 0, SubjectStyle = "signal", ObjectKind = "beacon", LootByTier = new() { ["Common"] = "a fragment of old star charts", ["Uncommon"] = "an encrypted data cache", ["Rare"] = "a lost explorer's black-box archive" } },
        new("SalvageSite", "Derelict Salvage", "A damaged vessel, {subject}, is drifting near the route. Scan it, secure the area, then recover the salvage - or pass.",
            [
                new() { ChoiceId = "scan", Label = "Scan the wreck", ContinuesEvent = true, GrantsFlag = "scanned", RevealsRarity = true, Story = "{crew} sweeps the sensors over {subject}. Warm signatures flicker behind the cargo hold - it looks like {loot}." },
                new() { ChoiceId = "secure", Label = "Secure the area", ContinuesEvent = true, GrantsFlag = "secured", SuccessChanceModifierPercent = 20, RewardMultiplierPercent = 80, RiskMultiplierPercent = 50, Story = "{crewlist} lock docking clamps onto {subject} and seal its breached corridors." },
                new() { ChoiceId = "salvage", Label = "Recover salvage", Story = "{crew} boards {subject} and starts cutting toward the cargo hold." },
                new() { ChoiceId = "pass", Label = "Pass it by", EndsWithoutReward = true, Story = "The crew leaves {subject} to drift on." }
            ], "pass", "The hatch gives way and the crew hauls out {loot}.", "A reactor surge rips through {subject}. The crew drops {loot} and flees.")
        { SortOrder = 1, SubjectStyle = "ship", ObjectKind = "wreck", AllowedRoutes = ["Uncharted", "Survey Route"], LootByTier = new() { ["Common"] = "salvage plating and spare parts", ["Uncommon"] = "a sealed navigation core", ["Rare"] = "an intact cryo-vault" } },
        new("Hazard", "Navigation Hazard", "{Subject} has appeared across the current flight path.",
            [
                new() { ChoiceId = "maneuver", Label = "Maneuver around it", Story = "{crew} takes the helm and arcs around {subject}." },
                new() { ChoiceId = "shield", Label = "Raise shields", SuccessChanceModifierPercent = 20, RewardMultiplierPercent = 80, Story = "{crew} diverts power to the shields as {subject} closes in." },
                new() { ChoiceId = "hold", Label = "Hold course", SuccessChanceModifierPercent = -15, RewardMultiplierPercent = 130, Story = "{crew} grits their teeth and holds course through {subject}." }
            ], "maneuver", "The crew clears {subject} and scoops up {loot}.", "{Subject} clips the hull and cargo is lost.")
        { SortOrder = 2, SubjectStyle = "hazard", ObjectKind = "debris", AllowedRoutes = ["Uncharted", "Direct Route"], LootByTier = new() { ["Common"] = "scattered ore chunks", ["Uncommon"] = "a cluster of crystal shards", ["Rare"] = "a glowing rare-earth nugget" } },
        new("RouteChoice", "Choose a Route", "The crew can take a direct route or detour through a survey area.",
            [new() { ChoiceId = "direct", Label = "Take the direct route" }, new() { ChoiceId = "detour", Label = "Take the survey detour" }], "direct")
        { SortOrder = 3, ObjectKind = "fork", LootByTier = new() { ["Common"] = "some useful scrap", ["Uncommon"] = "a valuable cache", ["Rare"] = "a priceless find" } },
        new("StationStop", "Space Station", "A station offers a brief resupply and banking stop.",
            [new() { ChoiceId = "resupply", Label = "Resupply" }, new() { ChoiceId = "repair", Label = "Repair the ship" }, new() { ChoiceId = "continue", Label = "Continue the voyage" }], "resupply")
        { SortOrder = 4, ObjectKind = "station", RewardProfileId = "" }
    ];

    public static bool IsStructural(string eventType) => eventType is "RouteChoice" or "StationStop";

    public static StarshipEventDefinition Select(IReadOnlyList<StarshipEventDefinition> catalog,
        StarshipExpeditionSnapshot snapshot, int sequence)
    {
        if (sequence == 1) return catalog.Single(definition => definition.EventType == "RouteChoice");
        if (snapshot.CurrentLeg > 0 && snapshot.StationIntervalLegs > 0 && snapshot.CurrentLeg % snapshot.StationIntervalLegs == 0)
            return catalog.Single(definition => definition.EventType == "StationStop");
        var candidates = catalog.Where(definition => !IsStructural(definition.EventType) && IsAvailable(definition, snapshot.CurrentRoute, snapshot.CurrentSector))
            .OrderBy(definition => definition.SortOrder).ThenBy(definition => definition.EventType, StringComparer.Ordinal).ToList();
        if (candidates.Count == 0) throw new InvalidOperationException("No Starship event is eligible for the current route and sector.");
        var total = candidates.Sum(definition => (long)definition.Weight);
        if (total > int.MaxValue) throw new InvalidOperationException("Starship event weights exceed the supported draw range.");
        var roll = StarshipExpeditionStory.StableIndex(snapshot.Seed, sequence, $"event-type:{snapshot.CurrentRoute}", (int)total);
        foreach (var definition in candidates)
        {
            if (roll < definition.Weight) return definition;
            roll -= definition.Weight;
        }
        throw new InvalidOperationException("Starship event selection failed.");
    }

    private static bool IsAvailable(StarshipEventDefinition definition, string route, string sector) =>
        definition.Enabled && definition.Weight > 0 &&
        (definition.AllowedRoutes.Count == 0 || definition.AllowedRoutes.Contains(route, StringComparer.OrdinalIgnoreCase)) &&
        (definition.AllowedSectors.Count == 0 || definition.AllowedSectors.Contains(sector, StringComparer.OrdinalIgnoreCase));

    public static void Validate(IReadOnlyList<StarshipEventDefinition> catalog, StarshipExpeditionSettings settings)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var defaults = CreateDefaults();
        var builtInIds = defaults.Select(definition => definition.EventType).ToHashSet(StringComparer.Ordinal);
        foreach (var definition in catalog)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.EventType) || definition.EventType.Length > 80 ||
                !ids.Add(definition.EventType) || !builtInIds.Contains(definition.EventType) && !SlugPattern().IsMatch(definition.EventType))
                throw new ArgumentException("Event IDs must be unique slugs of 1-80 characters; built-in IDs are reserved.");
            ValidateText(definition.Title, 100, true, false);
            ValidateText(definition.Description, 1000, true);
            ValidateText(definition.SuccessStory, 1000);
            ValidateText(definition.FailureStory, 1000);
            if (definition.Weight is < 1 or > 10 || definition.SortOrder < 0)
                throw new ArgumentException($"{definition.Title}: weight must be 1-10 and sort order non-negative.");
            if (definition.Choices == null || definition.Choices.Count is < 2 or > MaximumChoices)
                throw new ArgumentException($"{definition.Title}: use 2-{MaximumChoices} choices.");
            var choiceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var choice in definition.Choices)
            {
                if (choice == null || !SlugPattern().IsMatch(choice.ChoiceId ?? "") || choice.ChoiceId!.Length > 40 || !choiceIds.Add(choice.ChoiceId))
                    throw new ArgumentException($"{definition.Title}: choice IDs must be unique slugs.");
                ValidateText(choice.Label, 80, true, false);
                ValidateText(choice.Story, 1000);
                ValidateText(choice.Hint, 160, false, false);
                if (!labels.Add(choice.Label.Trim())) throw new ArgumentException($"{definition.Title}: choice labels must be unique.");
                if (choice.IsRisky || choice.SuccessChanceModifierPercent is < -50 or > 50 ||
                    choice.RewardMultiplierPercent is < 10 or > 300 || choice.RiskMultiplierPercent is < 0 or > 300)
                    throw new ArgumentException($"{definition.Title}: choice effects exceed their allowed bounds.");
                if (!string.IsNullOrEmpty(choice.GrantsFlag) && (!SlugPattern().IsMatch(choice.GrantsFlag) || choice.GrantsFlag.Length > 40 || !flags.Add(choice.GrantsFlag)))
                    throw new ArgumentException($"{definition.Title}: flags must be unique slugs.");
                if (choice.ContinuesEvent && (string.IsNullOrEmpty(choice.GrantsFlag) || choice.EndsWithoutReward))
                    throw new ArgumentException($"{definition.Title}: continuing choices must grant a flag and cannot end the event.");
            }
            if (!definition.Choices.Any(choice => choice.ChoiceId == definition.DefaultChoiceId && !choice.ContinuesEvent && choice.GrantsFlag.Length == 0))
                throw new ArgumentException($"{definition.Title}: choose a terminal, always-available default.");
            var binding = definition.RewardProfileId ?? settings.EventRewardProfileIds.GetValueOrDefault(definition.EventType, "");
            if (binding.Length > 0 && !settings.RewardProfiles.Any(profile => profile.Id == binding))
                throw new ArgumentException($"{definition.Title}: the reward profile does not exist.");
            if (!builtInIds.Contains(definition.EventType) && definition.Choices.Any(choice => choice.RevealsRarity) && binding.Length == 0)
                throw new ArgumentException($"{definition.Title}: revealing rarity requires a reward profile.");
            if (definition.AllowedRoutes == null || definition.AllowedRoutes.Any(route => !Routes.Contains(route, StringComparer.Ordinal)))
                throw new ArgumentException($"{definition.Title}: unknown route filter.");
            if (definition.AllowedSectors == null || definition.AllowedSectors.Any(sector => !StarshipExpeditionSettings.BuiltInSectors.Concat(settings.CustomSectors).Contains(sector, StringComparer.OrdinalIgnoreCase)))
                throw new ArgumentException($"{definition.Title}: unknown sector filter.");
            if (definition.SubjectStyle is not ("" or "ship" or "signal" or "hazard" or "custom"))
                throw new ArgumentException($"{definition.Title}: unknown subject style.");
            if (definition.SubjectNames == null || definition.Images == null || definition.LootByTier == null)
                throw new ArgumentException($"{definition.Title}: subject, image and loot lists cannot be null.");
            foreach (var name in definition.SubjectNames) ValidateText(name, 100, true, false);
            if (definition.SubjectStyle == "custom" && definition.SubjectNames.Count == 0)
                throw new ArgumentException($"{definition.Title}: custom subject style requires names.");
            if (definition.ObjectKind is not ("" or "wreck" or "beacon" or "debris" or "station" or "fork"))
                throw new ArgumentException($"{definition.Title}: unknown object kind.");
            if (definition.Images.Count > 12 || definition.Images.Any(image => !IsImageUrl(image)))
                throw new ArgumentException($"{definition.Title}: use at most 12 media-library or HTTP(S) images.");
            foreach (var loot in definition.LootByTier)
            {
                ValidateText(loot.Key, 24, true, false);
                ValidateText(loot.Value, 160, true, false);
            }
            if (IsStructural(definition.EventType)) ValidateStructural(definition, defaults.Single(item => item.EventType == definition.EventType));
        }
        foreach (var structural in defaults.Where(definition => IsStructural(definition.EventType)))
            if (!ids.Contains(structural.EventType)) throw new ArgumentException($"The catalog requires {structural.EventType}.");
        foreach (var route in Routes)
            foreach (var sector in settings.GetEffectiveSectors())
                if (!catalog.Any(definition => !IsStructural(definition.EventType) && IsAvailable(definition, route, sector)))
                    throw new ArgumentException($"No event is eligible for {route} in {sector}.");
    }

    private static void ValidateStructural(StarshipEventDefinition definition, StarshipEventDefinition baseline)
    {
        if (!definition.Enabled || definition.Weight != 1 || definition.DefaultChoiceId != baseline.DefaultChoiceId ||
            definition.AllowedRoutes.Count > 0 || definition.AllowedSectors.Count > 0 ||
            definition.Choices.Count != baseline.Choices.Count || definition.RewardProfileId != baseline.RewardProfileId)
            throw new ArgumentException($"{definition.EventType}: only wording and art can be customized.");
        for (var index = 0; index < baseline.Choices.Count; index++)
        {
            var choice = definition.Choices[index];
            if (choice.ChoiceId != baseline.Choices[index].ChoiceId || choice.ContinuesEvent || choice.IsRisky ||
                choice.EndsWithoutReward || choice.RevealsRarity || choice.GrantsFlag.Length > 0 ||
                choice.SuccessChanceModifierPercent != 0 || choice.RewardMultiplierPercent != 100 || choice.RiskMultiplierPercent != 100)
                throw new ArgumentException($"{definition.EventType}: structural choice mechanics cannot be changed.");
        }
    }

    private static void ValidateText(string? value, int maximum, bool required = false, bool templates = true)
    {
        if (value == null || value.Length > maximum || required && string.IsNullOrWhiteSpace(value) ||
            value.Split('\n').Any(line => line.TrimStart().StartsWith('/') || line.TrimStart().StartsWith('.') || line.TrimStart().StartsWith('!')) ||
            value.Any(character => char.IsControl(character) && character != '\n'))
            throw new ArgumentException($"Event text must be safe plain text of at most {maximum} characters.");
        var stripped = TokenPattern().Replace(value, match =>
        {
            if (!templates || !Tokens.Contains(match.Groups[1].Value)) throw new ArgumentException("Unknown event story token.");
            return "";
        });
        if (stripped.Contains('{') || stripped.Contains('}')) throw new ArgumentException("Unbalanced event story token.");
    }

    private static bool IsImageUrl(string? value) => value != null && value.Length <= 2048 &&
        (value.StartsWith("/media/", StringComparison.Ordinal) && !value.Contains("..", StringComparison.Ordinal) && !value.Contains('\\') && !value.Any(char.IsControl) ||
         Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo));

    [GeneratedRegex("^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
    [GeneratedRegex("\\{([^{}]*)\\}")]
    private static partial Regex TokenPattern();
}