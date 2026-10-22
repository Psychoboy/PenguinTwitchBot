using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

/// <summary>
/// Deterministic flavour text for events: generated subject names, crew callouts and loot descriptions.
/// Templates support {crew}, {crewlist}, {subject}, {Subject}, {loot}, {rarity} and {sector}.
/// </summary>
public static class StarshipExpeditionStory
{
    private static readonly string[] ShipPrefixes = ["ISV", "MV", "RSS", "UEV", "SS"];
    private static readonly string[] ShipAdjectives = ["Quiet", "Gilded", "Wandering", "Forgotten", "Ember", "Hollow", "Silver", "Stubborn", "Fading", "Restless"];
    private static readonly string[] ShipNouns = ["Meridian", "Kestrel", "Lantern", "Harbinger", "Marigold", "Odyssey", "Tidewater", "Larkspur", "Cormorant", "Aurora"];
    private static readonly string[] SignalWords = ["Kestrel", "Vesper", "Orrin", "Tamsin", "Halcyon", "Brigid", "Corvus", "Ilex"];
    private static readonly string[] SignalKinds = ["beacon", "transponder", "distress loop", "pulse", "relay"];
    private static readonly string[] HazardAdjectives = ["ragged", "glittering", "drifting", "tumbling", "silent"];
    private static readonly string[] HazardKinds = ["debris field", "micro-meteor swarm", "ion storm", "wreckage cloud"];

    private static readonly string[] ShipFirstNames = ["Emperor", "Rockhopper", "Macaroni", "Adelie", "Chinstrap", "Gentoo", "King", "Fairy", "Humboldt", "Snares"];
    private static readonly string[] ShipLastNames = ["Voyager", "Glider", "Skua", "Drifter", "Harbinger", "Comet", "Pathfinder", "Wayfarer"];
    private static readonly string[] HullStyles = ["arrow", "hauler", "scout", "cruiser"];
    private static readonly string[] AccentColors = ["#66d0c2", "#f4bd68", "#ef866f", "#8fb8ff", "#b79cff", "#8be28b"];

    public static StarshipShipSnapshot GenerateShip(int seed) => new()
    {
        Name = $"{ShipFirstNames[StableIndex(seed, 0, "ship-first", ShipFirstNames.Length)]} {ShipLastNames[StableIndex(seed, 0, "ship-last", ShipLastNames.Length)]}",
        HullStyle = HullStyles[StableIndex(seed, 0, "ship-hull", HullStyles.Length)],
        AccentColor = AccentColors[StableIndex(seed, 0, "ship-accent", AccentColors.Length)],
        Variant = StableIndex(seed, 0, "ship-variant", int.MaxValue)
    };

    public const int ObjectVariantCount = 3;

    public static string GetObjectKind(string eventType) => eventType switch
    {
        "SalvageSite" => "wreck",
        "SignalDetected" => "beacon",
        "Hazard" => "debris",
        "StationStop" => "station",
        "RouteChoice" => "fork",
        _ => string.Empty
    };

    public static int StableIndex(int seed, int sequence, string purpose, int count)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}:{sequence}:{purpose}"));
        return (int)(BinaryPrimitives.ReadUInt32LittleEndian(hash) % (uint)count);
    }

    public static long StableLongRange(int seed, int sequence, string purpose, long minimum, long maximum)
    {
        if (minimum < 0 || maximum < minimum)
            throw new ArgumentOutOfRangeException(nameof(minimum), "Reward range must be non-negative and ordered.");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}:{sequence}:{purpose}"));
        var range = (ulong)(maximum - minimum) + 1UL;
        return minimum + (long)(BinaryPrimitives.ReadUInt64LittleEndian(hash) % range);
    }

    public static string GenerateSubject(int seed, int sequence, string eventType)
    {
        string Pick(string[] items, string purpose) => items[StableIndex(seed, sequence, $"story-{purpose}", items.Length)];
        return eventType switch
        {
            "SalvageSite" => $"the {Pick(ShipPrefixes, "prefix")} {Pick(ShipAdjectives, "adjective")} {Pick(ShipNouns, "noun")}",
            "SignalDetected" => $"the {Pick(SignalWords, "word")}-{StableIndex(seed, sequence, "story-number", 9) + 1} {Pick(SignalKinds, "kind")}",
            "Hazard" => $"the {Pick(HazardAdjectives, "adjective")} {Pick(HazardKinds, "kind")}",
            _ => string.Empty
        };
    }

    public static string GenerateSubject(int seed, int sequence, StarshipEventDefinition definition)
    {
        if (definition.SubjectNames.Count > 0)
            return definition.SubjectNames[StableIndex(seed, sequence, "story-custom-subject", definition.SubjectNames.Count)];
        var style = definition.SubjectStyle switch
        {
            "ship" => "SalvageSite",
            "signal" => "SignalDetected",
            "hazard" => "Hazard",
            _ => definition.EventType
        };
        return GenerateSubject(seed, sequence, style);
    }

    public static string DescribeLoot(string eventType, string? rarity)
    {
        var definition = StarshipEventCatalog.CreateDefaults().FirstOrDefault(item => item.EventType == eventType);
        if (rarity != null && definition?.LootByTier.TryGetValue(rarity, out var text) == true) return text;
        return rarity switch
        {
            "Common" => "some useful scrap",
            "Uncommon" => "a valuable cache",
            "Rare" => "a priceless find",
            { Length: > 0 } => $"a {rarity.ToLowerInvariant()} find",
            _ => "something valuable"
        };
    }

    /// <summary>Renders a story template; names pick a deterministic hero among the people who chose the option.</summary>
    public static string Render(
        string template,
        StarshipExpeditionEventSnapshot expeditionEvent,
        int seed,
        IReadOnlyList<string> crewNames,
        string? rarity)
    {
        if (string.IsNullOrWhiteSpace(template)) return string.Empty;
        var subject = expeditionEvent.Subject;
        var hero = crewNames.Count == 0
            ? "The crew"
            : crewNames[StableIndex(seed, expeditionEvent.Sequence, $"story-hero:{expeditionEvent.StepNumber}", crewNames.Count)];
        var others = crewNames.Count - 1;
        var crewList = crewNames.Count switch
        {
            0 => "the crew",
            1 => crewNames[0],
            2 => $"{crewNames[0]} and {crewNames[1]}",
            _ => $"{hero} and {others} others"
        };
        var capitalizedSubject = subject.Length == 0 ? subject : char.ToUpperInvariant(subject[0]) + subject[1..];

        var loot = expeditionEvent.LootByTier == null
            ? DescribeLoot(expeditionEvent.EventType, rarity)
            : expeditionEvent.LootByTier.FirstOrDefault(item => string.Equals(item.Key, rarity, StringComparison.OrdinalIgnoreCase)).Value
                ?? (string.IsNullOrEmpty(rarity) ? "something valuable" : $"a {rarity.ToLowerInvariant()} find");
        return template
            .Replace("{crewlist}", crewList, StringComparison.Ordinal)
            .Replace("{crew}", hero, StringComparison.Ordinal)
            .Replace("{Subject}", capitalizedSubject, StringComparison.Ordinal)
            .Replace("{subject}", subject, StringComparison.Ordinal)
            .Replace("{loot}", loot, StringComparison.Ordinal)
            .Replace("{rarity}", rarity ?? "unknown", StringComparison.Ordinal)
            .Replace("{sector}", expeditionEvent.Sector, StringComparison.Ordinal);
    }

    public static IReadOnlyList<string> GetVoterNames(
        StarshipExpeditionSnapshot snapshot,
        StarshipExpeditionEventSnapshot expeditionEvent,
        string choiceId) =>
        expeditionEvent.Votes
            .Where(vote => string.Equals(vote.ChoiceId, choiceId, StringComparison.OrdinalIgnoreCase))
            .Select(vote => snapshot.Crew.FirstOrDefault(member =>
                string.Equals(member.UserId, vote.UserId, StringComparison.OrdinalIgnoreCase))?.DisplayName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static string BuildOutcomeStory(StarshipExpeditionSnapshot snapshot, StarshipExpeditionEventSnapshot resolvedEvent)
    {
        var finalChoice = resolvedEvent.Choices.FirstOrDefault(choice =>
            string.Equals(choice.ChoiceId, resolvedEvent.ResolvedChoiceId, StringComparison.OrdinalIgnoreCase));
        var voters = GetVoterNames(snapshot, resolvedEvent, resolvedEvent.ResolvedChoiceId ?? string.Empty);
        var rarity = string.IsNullOrEmpty(resolvedEvent.OutcomeRarity) ? resolvedEvent.RevealedRarity : resolvedEvent.OutcomeRarity;
        var parts = new List<string> { Render(finalChoice?.Story ?? string.Empty, resolvedEvent, snapshot.Seed, voters, rarity) };
        var outcomeTemplate = resolvedEvent.OutcomeResult switch
        {
            StarshipExpeditionNarrative.OutcomeSuccess => resolvedEvent.SuccessStory,
            StarshipExpeditionNarrative.OutcomeFailure => resolvedEvent.FailureStory,
            _ => string.Empty
        };
        parts.Add(Render(outcomeTemplate, resolvedEvent, snapshot.Seed, voters, rarity));
        return string.Join(" ", parts.Where(part => part.Length > 0));
    }
}
