using System.Text.RegularExpressions;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Services;

namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

public static partial class StarshipEventCatalog
{
    public const int MaximumChoices = 4;
    public const int CurrentRouteNameMigrationVersion = 1;
    public const string StartRoute = "Start";
    public const string UnchartedRoute = "Uncharted";
    public static IReadOnlyList<string> Routes { get; } = [StartRoute, "Direct Route", "Survey Route", UnchartedRoute];
    public static IReadOnlyList<string> SubjectStyles { get; } = ["", "ship", "signal", "hazard", "custom"];
    public static IReadOnlyList<string> ObjectKinds { get; } = ["", .. StarshipAppearanceSettings.ObjectKinds];
    private static readonly HashSet<string> BuiltInIds = CreateDefaults().Select(definition => definition.EventType).ToHashSet(StringComparer.Ordinal);
    private static readonly HashSet<string> Tokens = new(StringComparer.Ordinal)
        { "crew", "crewlist", "subject", "Subject", "loot", "rarity", "sector" };

    public static List<StarshipEventDefinition> CreateDefaults() =>
    [
        new("SignalDetected", "Unidentified Signal", "A signal from {subject} is repeating at the edge of sensor range. Scan it, trace its origin, then investigate - or move on.",
            [], "", "It was real: the crew recovers {loot}.", "The signal was a trap. The ship limps home damaged and empty.")
        {
            SortOrder = 0, SubjectStyle = "signal", ObjectKind = "beacon", ShipFailureEffect = GetDefaultShipFailureEffect("SignalDetected"),
            StartStepId = "approach", DefaultStepSuppliesCost = 0,
            Steps =
            [
                VoteStep("approach", "The signal repeats from {subject}. How should the crew investigate?", "pass",
                    new() { ChoiceId = "scan", Label = "Scan the signal", NextStepId = "scan-report", RevealsRarity = true, Story = "{crew} sweeps the signal for a reply." },
                    new() { ChoiceId = "trace", Label = "Trace its origin", NextStepId = "trace-report", SuccessChanceModifierPercent = 15, RewardMultiplierPercent = 85, RiskMultiplierPercent = 60, Story = "{crewlist} triangulate {subject} across the {sector}." },
                    new() { ChoiceId = "investigate", Label = "Investigate in person", NextStepId = "investigation", Story = "{crew} takes a shuttle out to {subject}." },
                    new() { ChoiceId = "pass", Label = "Continue on course", NextStepId = "pass-ending", EndsWithoutReward = true, Story = "The crew lets {subject} fade into static." }),
                AutomaticStep("scan-report", "The scan returns a faint response.", "The message is a plea in a long-dead dialect. Its source lies deeper in the signal field.", "analysis", 3),
                VoteStep("analysis", "The crew has a partial translation. What now?", "decode",
                    new() { ChoiceId = "decode", Label = "Decode the message", NextStepId = "recovery-ending", Story = "{crew} follows the coordinates to {loot}." },
                    new() { ChoiceId = "triangulate", Label = "Track the carrier signal", NextStepId = "ambush", RiskMultiplierPercent = 140, Story = "{crewlist} follow the signal into a narrow approach." },
                    new() { ChoiceId = "withdraw", Label = "Withdraw safely", NextStepId = "pass-ending", EndsWithoutReward = true, Story = "The crew records the signal and turns away." }),
                AutomaticStep("trace-report", "The signal resolves to a hidden dock.", "The dock is silent, but its beacon is still transmitting.", "ambush", 4),
                VoteStep("investigation", "The shuttle reaches the beacon. How does the crew proceed?", "secure",
                    new() { ChoiceId = "secure", Label = "Secure the beacon", NextStepId = "recovery-ending", SuccessChanceModifierPercent = 10, RiskMultiplierPercent = 60 },
                    new() { ChoiceId = "rush", Label = "Rush the transmitter", NextStepId = "recovery-ending", SuccessChanceModifierPercent = -15, RewardMultiplierPercent = 150, RiskMultiplierPercent = 160 },
                    new() { ChoiceId = "retreat", Label = "Recall the shuttle", NextStepId = "pass-ending", EndsWithoutReward = true }),
                VoteStep("ambush", "The dock's defenses wake up. What is the crew's response?", "evade",
                    new() { ChoiceId = "evade", Label = "Evade and recover the archive", NextStepId = "recovery-ending", SuccessChanceModifierPercent = 10, RiskMultiplierPercent = 50 },
                    new() { ChoiceId = "counter", Label = "Push through the defenses", NextStepId = "recovery-ending", SuccessChanceModifierPercent = -20, RewardMultiplierPercent = 160, RiskMultiplierPercent = 180 },
                    new() { ChoiceId = "retreat", Label = "Break off the approach", NextStepId = "pass-ending", EndsWithoutReward = true }),
                EndStep("recovery-ending", "The crew returns with {loot}.", "The signal was real: the crew recovers {loot}."),
                EndStep("pass-ending", "The crew turns away from the signal.")
            ],
            LootByTier = new() { ["Common"] = "a fragment of old star charts", ["Uncommon"] = "an encrypted data cache", ["Rare"] = "a lost explorer's black-box archive" }
        },
        new("SalvageSite", "Derelict Salvage", "A damaged vessel, {subject}, is drifting near the route. Scan it, secure the area, then recover the salvage - or pass.",
            [], "", "The hatch gives way and the crew hauls out {loot}.", "A reactor surge rips through {subject}. The crew drops {loot} and flees.")
        {
            SortOrder = 1, SubjectStyle = "ship", ObjectKind = "wreck", ShipFailureEffect = GetDefaultShipFailureEffect("SalvageSite"), AllowedRoutes = ["Uncharted", "Survey Route"],
            StartStepId = "approach", DefaultStepSuppliesCost = 0,
            Steps =
            [
                VoteStep("approach", "The damaged vessel {subject} is drifting nearby. Choose an approach.", "pass",
                    new() { ChoiceId = "scan", Label = "Scan the wreck", NextStepId = "scan-report", RevealsRarity = true, Story = "{crew} sweeps the sensors over {subject}." },
                    new() { ChoiceId = "secure", Label = "Secure the area", NextStepId = "secure-report", SuccessChanceModifierPercent = 10, RewardMultiplierPercent = 90, RiskMultiplierPercent = 50 },
                    new() { ChoiceId = "rush", Label = "Board the wreck now", NextStepId = "recovery", SuccessChanceModifierPercent = -15, RewardMultiplierPercent = 150, RiskMultiplierPercent = 160 },
                    new() { ChoiceId = "pass", Label = "Pass it by", NextStepId = "pass-ending", EndsWithoutReward = true, Story = "The crew leaves {subject} to drift on." }),
                AutomaticStep("scan-report", "The scan finds warm signatures behind the cargo hold.", "The wreck contains {loot}; its frame is still unstable.", "plan", 3),
                VoteStep("plan", "How should the crew handle the unstable wreck?", "secure",
                    new() { ChoiceId = "secure", Label = "Seal the breached corridors", NextStepId = "secure-report", SuccessChanceModifierPercent = 20, RewardMultiplierPercent = 80, RiskMultiplierPercent = 50 },
                    new() { ChoiceId = "rush", Label = "Cut straight to the cargo hold", NextStepId = "recovery", SuccessChanceModifierPercent = -10, RewardMultiplierPercent = 140, RiskMultiplierPercent = 150 },
                    new() { ChoiceId = "withdraw", Label = "Leave the wreck", NextStepId = "pass-ending", EndsWithoutReward = true }),
                AutomaticStep("secure-report", "The wreck is secured.", "{crewlist} lock docking clamps onto {subject} and seal its breached corridors.", "recovery", 4),
                VoteStep("recovery", "The cargo hold is reachable. What should the crew recover?", "salvage",
                    new() { ChoiceId = "salvage", Label = "Recover the salvage", NextStepId = "recovery-ending", RewardMultiplierPercent = 100, Story = "{crew} boards {subject} and cuts toward the cargo hold." },
                    new() { ChoiceId = "deep-cut", Label = "Cut into the reactor vault", NextStepId = "recovery-ending", SuccessChanceModifierPercent = -20, RewardMultiplierPercent = 170, RiskMultiplierPercent = 200 },
                    new() { ChoiceId = "leave", Label = "Return to the ship", NextStepId = "pass-ending", EndsWithoutReward = true }),
                EndStep("recovery-ending", "The crew hauls {loot} back aboard."),
                EndStep("pass-ending", "The crew leaves {subject} to drift on.")
            ],
            LootByTier = new() { ["Common"] = "salvage plating and spare parts", ["Uncommon"] = "a sealed navigation core", ["Rare"] = "an intact cryo-vault" }
        },
        new("Hazard", "Navigation Hazard", "{Subject} has appeared across the current flight path.",
            [], "", "The crew clears {subject} and scoops up {loot}.", "{Subject} clips the hull and cargo is lost.")
        {
            SortOrder = 2, SubjectStyle = "hazard", ObjectKind = "debris", ShipFailureEffect = GetDefaultShipFailureEffect("Hazard"), AllowedRoutes = ["Uncharted", "Direct Route"],
            StartStepId = "reaction", DefaultStepSuppliesCost = 0,
            Steps =
            [
                VoteStep("reaction", "{Subject} has appeared across the current flight path. React.", "maneuver",
                    new() { ChoiceId = "maneuver", Label = "Maneuver around it", NextStepId = "clear-ending", RiskMultiplierPercent = 50, Story = "{crew} takes the helm and arcs around {subject}." },
                    new() { ChoiceId = "shield", Label = "Raise shields", NextStepId = "shield-report", SuccessChanceModifierPercent = 15, RewardMultiplierPercent = 80, RiskMultiplierPercent = 50 },
                    new() { ChoiceId = "hold", Label = "Hold course", NextStepId = "impact-ending", SuccessChanceModifierPercent = -15, RewardMultiplierPercent = 150, RiskMultiplierPercent = 180, Story = "{crew} grits their teeth and holds course through {subject}." }),
                AutomaticStep("shield-report", "The shields absorb the first impact.", "{crew} diverts power to the shields as {subject} closes in.", "follow-up", 3),
                VoteStep("follow-up", "The ship is through the first impact. What is the next move?", "clear",
                    new() { ChoiceId = "clear", Label = "Finish the maneuver", NextStepId = "clear-ending", SuccessChanceModifierPercent = 10, RiskMultiplierPercent = 50 },
                    new() { ChoiceId = "push", Label = "Push through the densest debris", NextStepId = "clear-ending", SuccessChanceModifierPercent = -20, RewardMultiplierPercent = 170, RiskMultiplierPercent = 200 },
                    new() { ChoiceId = "abort", Label = "Abort the route", NextStepId = "abort-ending", EndsWithoutReward = true }),
                EndStep("clear-ending", "The ship clears {subject} and recovers {loot}."),
                EndStep("impact-ending", "{Subject} clips the hull and cargo is lost."),
                EndStep("abort-ending", "The crew retreats from {subject} without a salvage run.")
            ],
            LootByTier = new() { ["Common"] = "scattered ore chunks", ["Uncommon"] = "a cluster of crystal shards", ["Rare"] = "a glowing rare-earth nugget" }
        },
        new("RouteChoice", "Choose a Route", "The crew can take a direct route or detour through a survey area.",
            [new() { ChoiceId = "direct", Label = "Take the direct route" }, new() { ChoiceId = "detour", Label = "Take the survey detour" }], "direct")
        { SortOrder = 3, ObjectKind = "fork", LootByTier = new() { ["Common"] = "some useful scrap", ["Uncommon"] = "a valuable cache", ["Rare"] = "a priceless find" } },
        new("StationStop", "Space Station", "A station offers a brief resupply and banking stop.",
            [new() { ChoiceId = "resupply", Label = "Resupply" }, new() { ChoiceId = "repair", Label = "Repair the ship" }, new() { ChoiceId = "continue", Label = "Continue the voyage" }], "resupply")
        { SortOrder = 4, ObjectKind = "station", RewardProfileId = "" }
    ];

    public static bool IsStructural(string eventType) => eventType is "RouteChoice" or "StationStop";
    public static bool IsBuiltIn(string eventType) => BuiltInIds.Contains(eventType);
    public static DropGameSoundConfig GetDefaultOpenSound(string eventType) => eventType switch
    {
        "RouteChoice" => new() { Enabled = true, AudioFile = "/media/starship_route_choice.wav", Volume = 55 },
        "StationStop" => new() { Enabled = true, AudioFile = "/media/starship_station_dock.wav", Volume = 55 },
        "SignalDetected" => new() { Enabled = true, AudioFile = "/media/starship_signal_detected.wav", Volume = 50 },
        "SalvageSite" => new() { Enabled = true, AudioFile = "/media/starship_salvage_found.wav", Volume = 55 },
        "Hazard" => new() { Enabled = true, AudioFile = "/media/starship_hazard_warning.wav", Volume = 55 },
        "courier-relay" => new() { Enabled = true, AudioFile = "/media/starship_courier_relay.wav", Volume = 50 },
        "survey-cache" => new() { Enabled = true, AudioFile = "/media/starship_survey_cache.wav", Volume = 50 },
        "debris-crossing" => new() { Enabled = true, AudioFile = "/media/starship_debris_crossing.wav", Volume = 55 },
        _ => new() { Enabled = true, AudioFile = "/media/starship_custom_event.wav", Volume = 50 }
    };
    public static DropGameSoundConfig GetEffectiveOpenSound(StarshipEventDefinition? definition, string eventType, DropGameSoundConfig? typeDefault = null)
    {
        var sound = definition?.OpenSound;
        if (sound == null || sound.OverrideMode == DropGameSoundOverrideMode.Inherit)
            return typeDefault ?? GetDefaultOpenSound(eventType);
        if (sound.OverrideMode == DropGameSoundOverrideMode.Mute)
            return new() { Enabled = false, AudioFile = sound.AudioFile, Volume = sound.Volume };
        return new() { Enabled = sound.Enabled, AudioFile = sound.AudioFile, Volume = sound.Volume };
    }

    public static void ValidateSoundConfig(DropGameSoundConfig? sound, string context)
    {
        if (sound == null) throw new ArgumentException($"{context}: sound settings cannot be null.");
        if (!sound.Enabled || string.IsNullOrWhiteSpace(sound.AudioFile)) return;
        var path = sound.AudioFile.Trim();
        if (path.Length > StarshipAppearanceSettings.MaximumImageUrlLength || !path.StartsWith("/media/", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"{context}: choose an audio file from the Media Manager.");
        var fileName = Uri.UnescapeDataString(path["/media/".Length..]);
        if (Path.GetFileName(fileName) != fileName || !MediaUploadService.SupportedAudioExtensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"{context}: choose a supported audio file from the Media Manager.");
    }
    public static StarshipShipEffect? GetDefaultShipFailureEffect(string eventType) => eventType switch
    {
        "Hazard" or "debris-crossing" => new() { HullDelta = -25, SuppliesDelta = -10 },
        "SalvageSite" or "survey-cache" => new() { HullDelta = -20, SuppliesDelta = -10 },
        "SignalDetected" => new() { HullDelta = -15, SuppliesDelta = -10 },
        _ => null
    };

    public static StarshipEventDefinition Select(IReadOnlyList<StarshipEventDefinition> catalog,
        StarshipExpeditionSnapshot snapshot, int sequence)
    {
        if (sequence == 1) return catalog.Single(definition => definition.EventType == "RouteChoice");
        if (snapshot.CurrentLeg > 0 && StarshipExpeditionOverlaySnapshot.GetStationLegs(snapshot).Contains(snapshot.CurrentLeg + 1))
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
        foreach (var definition in catalog)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.EventType) || definition.EventType.Length > 80 ||
                !ids.Add(definition.EventType) || !IsBuiltIn(definition.EventType) && !SlugPattern().IsMatch(definition.EventType))
                throw new ArgumentException("Event IDs must be unique slugs of 1-80 characters; built-in IDs are reserved.");
            ValidateSoundConfig(definition.OpenSound == null ? null : new DropGameSoundConfig
            {
                Enabled = definition.OpenSound.Override && definition.OpenSound.Enabled,
                AudioFile = definition.OpenSound.AudioFile,
                Volume = definition.OpenSound.Volume
            }, $"{definition.Title} event sound");
            ValidateText(definition.Title, 100, true, false);
            ValidateText(definition.Description, 1000, true);
            ValidateText(definition.SuccessStory, 1000);
            ValidateText(definition.FailureStory, 1000);
            if (definition.SuccessChancePercent is < 0 or > 100)
                throw new ArgumentException($"{definition.Title}: success chance must be 0-100 percent.");
            definition.ShipSuccessEffect?.Validate();
            definition.ShipFailureEffect?.Validate();
            if (definition.ShipSuccessEffect?.SuppliesCost > 0 || definition.ShipFailureEffect?.SuppliesCost > 0)
                throw new ArgumentException($"{definition.Title}: outcome effects cannot require a supply payment.");
            if (definition.Weight is < 1 or > 10 || definition.SortOrder < 0)
                throw new ArgumentException($"{definition.Title}: weight must be 1-10 and sort order non-negative.");
            var isGraphEvent = definition.Steps is { Count: > 0 };
            if (isGraphEvent && definition.Choices.Count > 0)
                throw new ArgumentException($"{definition.Title}: graph event choices must belong to their steps.");
            var eventChoices = isGraphEvent
                ? StarshipEventGraphValidator.Validate(definition)
                : definition.Choices ?? throw new ArgumentException($"{definition.Title}: choices cannot be null.");
            if (!isGraphEvent && eventChoices.Count is < 2 or > MaximumChoices)
                throw new ArgumentException($"{definition.Title}: use 2-{MaximumChoices} choices.");
            var choiceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var choice in eventChoices)
            {
                if (choice == null || !SlugPattern().IsMatch(choice.ChoiceId ?? "") || choice.ChoiceId!.Length > 40 ||
                    !isGraphEvent && !choiceIds.Add(choice.ChoiceId))
                    throw new ArgumentException($"{definition.Title}: choice IDs must be unique slugs.");
                ValidateText(choice.Label, 80, true, false);
                ValidateText(choice.Story, 1000);
                ValidateText(choice.Hint, 160, false, false);
                choice.ShipEffect?.Validate();
                if (!isGraphEvent && !labels.Add(choice.Label.Trim())) throw new ArgumentException($"{definition.Title}: choice labels must be unique.");
                if (choice.IsRisky || choice.SuccessChanceModifierPercent is < -50 or > 50 ||
                    choice.RewardMultiplierPercent is < 10 or > 300 || choice.RiskMultiplierPercent is < 0 or > 300)
                    throw new ArgumentException($"{definition.Title}: choice effects exceed their allowed bounds.");
                if (!string.IsNullOrEmpty(choice.GrantsFlag) && (!SlugPattern().IsMatch(choice.GrantsFlag) || choice.GrantsFlag.Length > 40 || !flags.Add(choice.GrantsFlag)))
                    throw new ArgumentException($"{definition.Title}: flags must be unique slugs.");
                if (choice.ContinuesEvent && (string.IsNullOrEmpty(choice.GrantsFlag) || choice.EndsWithoutReward))
                    throw new ArgumentException($"{definition.Title}: continuing choices must grant a flag and cannot end the event.");
            }
            if (!isGraphEvent && !definition.Choices.Any(choice => choice.ChoiceId == definition.DefaultChoiceId && !choice.ContinuesEvent && choice.GrantsFlag.Length == 0))
                throw new ArgumentException($"{definition.Title}: choose a terminal, always-available default.");
            if (!isGraphEvent && definition.Choices.Single(choice => choice.ChoiceId == definition.DefaultChoiceId).ShipEffect?.SuppliesCost > 0)
                throw new ArgumentException($"{definition.Title}: the default choice cannot require supplies.");
            var binding = definition.RewardProfileId ?? settings.EventRewardProfileIds.GetValueOrDefault(definition.EventType, "");
            if (binding.Length > 0 && !settings.RewardProfiles.Any(profile => profile.Id == binding))
                throw new ArgumentException($"{definition.Title}: the reward profile does not exist.");
            if (!IsBuiltIn(definition.EventType) && eventChoices.Any(choice => choice.RevealsRarity) && binding.Length == 0)
                throw new ArgumentException($"{definition.Title}: revealing rarity requires a reward profile.");
            if (definition.AllowedRoutes == null || definition.AllowedRoutes.Any(route => !Routes.Contains(route, StringComparer.Ordinal)))
                throw new ArgumentException($"{definition.Title}: unknown route filter.");
            if (definition.AllowedSectors == null || definition.AllowedSectors.Any(sector => !StarshipExpeditionSettings.BuiltInSectors.Concat(settings.CustomSectors).Contains(sector, StringComparer.OrdinalIgnoreCase)))
                throw new ArgumentException($"{definition.Title}: unknown sector filter.");
            if (!SubjectStyles.Contains(definition.SubjectStyle, StringComparer.Ordinal))
                throw new ArgumentException($"{definition.Title}: unknown subject style.");
            if (definition.SubjectNames == null || definition.Images == null || definition.LootByTier == null)
                throw new ArgumentException($"{definition.Title}: subject, image and loot lists cannot be null.");
            foreach (var name in definition.SubjectNames) ValidateText(name, 100, true, false);
            if (definition.SubjectStyle == "custom" && definition.SubjectNames.Count == 0)
                throw new ArgumentException($"{definition.Title}: custom subject style requires names.");
            if (!ObjectKinds.Contains(definition.ObjectKind, StringComparer.Ordinal))
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
        if (definition.Steps.Count > 0 || !definition.Enabled || definition.Weight != 1 || definition.DefaultChoiceId != baseline.DefaultChoiceId ||
            definition.AllowedRoutes.Count > 0 || definition.AllowedSectors.Count > 0 ||
            definition.Choices.Count != baseline.Choices.Count || definition.RewardProfileId != baseline.RewardProfileId ||
            definition.SuccessChancePercent != null || definition.ShipSuccessEffect != null || definition.ShipFailureEffect != null)
            throw new ArgumentException($"{definition.EventType}: only wording and art can be customized.");
        for (var index = 0; index < baseline.Choices.Count; index++)
        {
            var choice = definition.Choices[index];
            if (choice.ChoiceId != baseline.Choices[index].ChoiceId || choice.ContinuesEvent || choice.IsRisky || choice.ShipEffect != null ||
                choice.EndsWithoutReward || choice.RevealsRarity || choice.GrantsFlag.Length > 0 ||
                choice.SuccessChanceModifierPercent != 0 || choice.RewardMultiplierPercent != 100 || choice.RiskMultiplierPercent != 100)
                throw new ArgumentException($"{definition.EventType}: structural choice mechanics cannot be changed.");
        }
    }

    private static StarshipEventStepDefinition VoteStep(string stepId, string prompt, string defaultChoiceId,
        params StarshipEventChoiceSnapshot[] choices) => new()
    {
        StepId = stepId,
        Prompt = prompt,
        Type = StarshipEventStepType.Vote,
        DefaultChoiceId = defaultChoiceId,
        Choices = [.. choices]
    };

    private static StarshipEventStepDefinition AutomaticStep(string stepId, string prompt, string narrative,
        string nextStepId, int? suppliesCostOverride = null) => new()
    {
        StepId = stepId,
        Prompt = prompt,
        Narrative = narrative,
        Type = StarshipEventStepType.Automatic,
        NextStepId = nextStepId,
        SuppliesCostOverride = suppliesCostOverride
    };

    private static StarshipEventStepDefinition EndStep(string stepId, string prompt, string? narrative = null) => new()
    {
        StepId = stepId,
        Prompt = prompt,
        Narrative = narrative,
        Type = StarshipEventStepType.End
    };

    internal static void ValidateText(string? value, int maximum, bool required = false, bool templates = true)
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