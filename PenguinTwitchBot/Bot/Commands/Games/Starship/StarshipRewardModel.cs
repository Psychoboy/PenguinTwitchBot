using System.Text.RegularExpressions;

namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

/// <summary>One rarity level of a reward profile: how likely it is, what it pays, and what it runs.</summary>
public sealed class StarshipRewardTier
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#a8c1bd";
    /// <summary>Relative weight; the chance of a tier is its weight divided by the total of all enabled tiers.</summary>
    public int Weight { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public List<StarshipRewardEntry> Rewards { get; set; } = [];
    /// <summary>Actions run when the event succeeds at this tier.</summary>
    public List<StarshipRewardAction> Actions { get; set; } = [];
}

public sealed class StarshipRewardEntry
{
    public int PointTypeId { get; set; }
    public string PointTypeName { get; set; } = string.Empty;
    public long Minimum { get; set; }
    public long Maximum { get; set; }
    /// <summary>Chance this reward is granted when the tier is won; 100 means guaranteed.</summary>
    public int ChancePercent { get; set; } = 100;
}

public static class StarshipActionTimings
{
    public const string Immediate = "Immediate";
    public const string AfterBank = "AfterBank";
}

public sealed class StarshipRewardAction
{
    public int ActionId { get; set; }
    public string ActionName { get; set; } = string.Empty;
    /// <summary><see cref="StarshipActionTimings.Immediate"/> runs right after the event resolves; <see cref="StarshipActionTimings.AfterBank"/> runs once the crew's points are banked.</summary>
    public string Timing { get; set; } = StarshipActionTimings.Immediate;
}

public sealed class StarshipRewardCap
{
    public int PointTypeId { get; set; }
    public long? MaximumPerEvent { get; set; }
    public long? MaximumUnbankedPerVoyage { get; set; }
}

public sealed class StarshipLossRule
{
    public int PointTypeId { get; set; }
    public string PointTypeName { get; set; } = string.Empty;
    public int Percent { get; set; }
    public long? MaximumPerEvent { get; set; }
}

public sealed partial class StarshipRewardProfile
{
    public const int MaximumTiers = 12;
    public const int MaximumRewardsPerTier = 6;
    public const int MaximumActionsPerList = 10;
    public const int MaximumLossRules = 6;
    public const int MaximumTierNameLength = 24;
    public static readonly long? DefaultMaximumPerEvent = null;
    public static readonly long? DefaultMaximumUnbankedPerVoyage = null;

    public static IReadOnlyList<string> ActionTimings { get; } = [StarshipActionTimings.Immediate, StarshipActionTimings.AfterBank];

    public static StarshipRewardProfile? FindForEvent(IEnumerable<StarshipRewardProfile> profiles,
        IReadOnlyDictionary<string, string> bindings, string eventType)
    {
        var binding = bindings.FirstOrDefault(item => string.Equals(item.Key, eventType, StringComparison.OrdinalIgnoreCase));
        return profiles.FirstOrDefault(profile => profile.Enabled && (binding.Key != null
            ? string.Equals(profile.Id, binding.Value, StringComparison.OrdinalIgnoreCase)
            : string.Equals(profile.EventType, eventType, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>True for profiles saved before tiers existed; their legacy fields are read as three tiers.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool UsesLegacyFields => Tiers.Count == 0;

    public IReadOnlyList<StarshipRewardTier> GetTiers()
    {
        if (!UsesLegacyFields) return Tiers;
        return
        [
            LegacyTier("common", "Common", "#a8c1bd", CommonWeightPercent, CommonMinimum, CommonMaximum),
            LegacyTier("uncommon", "Uncommon", "#8be28b", UncommonWeightPercent, UncommonMinimum, UncommonMaximum),
            LegacyTier("rare", "Rare", "#b79cff", RareWeightPercent, RareMinimum, RareMaximum)
        ];
    }

    public StarshipRewardCap GetCap(int pointTypeId) =>
        Caps.FirstOrDefault(cap => cap.PointTypeId == pointTypeId) ?? new StarshipRewardCap
        {
            PointTypeId = pointTypeId,
            MaximumPerEvent = UsesLegacyFields ? MaximumPerEvent : null,
            MaximumUnbankedPerVoyage = UsesLegacyFields ? MaximumUnbankedPerVoyage : null
        };

    public IReadOnlyList<StarshipLossRule> GetLossRules() => UsesLegacyFields
        ? [new StarshipLossRule { PointTypeId = PointTypeId, Percent = RiskLossPercent, MaximumPerEvent = MaximumRiskLossPerEvent }]
        : LossRules;

    /// <summary>The point type shown in summaries: the first one any tier pays, else the first loss rule's.</summary>
    public int GetPrimaryPointTypeId()
    {
        if (UsesLegacyFields) return PointTypeId;
        return Tiers.SelectMany(tier => tier.Rewards).Select(reward => reward.PointTypeId).FirstOrDefault(id => id > 0) is var rewardType && rewardType > 0
            ? rewardType
            : LossRules.Select(rule => rule.PointTypeId).FirstOrDefault(id => id > 0);
    }

    /// <summary>Every point type this profile can pay, take away or cap.</summary>
    public IReadOnlyList<int> GetPointTypeIds() => GetTiers()
        .SelectMany(tier => tier.Rewards.Select(reward => reward.PointTypeId))
        .Concat(GetLossRules().Select(rule => rule.PointTypeId))
        .Where(id => id > 0)
        .Distinct()
        .ToList();

    public string GetPointTypeName(int pointTypeId)
    {
        var name = GetTiers().SelectMany(tier => tier.Rewards).FirstOrDefault(reward => reward.PointTypeId == pointTypeId && reward.PointTypeName.Length > 0)?.PointTypeName;
        return name ?? LossRules.FirstOrDefault(rule => rule.PointTypeId == pointTypeId && !string.IsNullOrWhiteSpace(rule.PointTypeName))?.PointTypeName
            ?? (pointTypeId == PointTypeId ? PointTypeName : string.Empty);
    }

    private StarshipRewardTier LegacyTier(string id, string name, string color, int weight, long minimum, long maximum) => new()
    {
        Id = id,
        Name = name,
        Color = color,
        Weight = weight,
        Rewards = [new StarshipRewardEntry { PointTypeId = PointTypeId, PointTypeName = PointTypeName, Minimum = minimum, Maximum = maximum }]
    };

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    internal static partial Regex ColorPattern();

    /// <summary>Converts a legacy profile into the explicit tier shape (a no-op for profiles already using tiers).</summary>
    public void ConvertLegacyToTiers()
    {
        if (!UsesLegacyFields) return;
        var tiers = GetTiers().Select(tier => new StarshipRewardTier
        {
            Id = tier.Id,
            Name = tier.Name,
            Color = tier.Color,
            Weight = tier.Weight,
            Rewards = tier.Rewards.Select(reward => new StarshipRewardEntry
            {
                PointTypeId = reward.PointTypeId,
                PointTypeName = reward.PointTypeName,
                Minimum = reward.Minimum,
                Maximum = reward.Maximum,
                ChancePercent = reward.ChancePercent
            }).ToList()
        }).ToList();
        var caps = new List<StarshipRewardCap> { GetCap(PointTypeId) };
        var loss = GetLossRules().Select(rule => new StarshipLossRule
        {
            PointTypeId = rule.PointTypeId, Percent = rule.Percent, MaximumPerEvent = rule.MaximumPerEvent
        }).ToList();
        Tiers = tiers;
        Caps = caps;
        LossRules = loss;
    }

    public void ValidateTiers(string label)
    {
        if (UsesLegacyFields) return;
        if (Tiers.Count > MaximumTiers)
            throw new ArgumentException($"{label}: use at most {MaximumTiers} rarity tiers.", nameof(Tiers));

        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tier in Tiers)
        {
            tier.Name = (tier.Name ?? string.Empty).Trim();
            if (tier.Name.Length is < 1 or > MaximumTierNameLength)
                throw new ArgumentException($"{label}: tier names must be 1-{MaximumTierNameLength} characters.", nameof(Tiers));
            if (!usedNames.Add(tier.Name))
                throw new ArgumentException($"{label}: there are two tiers named {tier.Name}.", nameof(Tiers));
            if (!ColorPattern().IsMatch(tier.Color ?? string.Empty))
                throw new ArgumentException($"{label}: tier {tier.Name} needs a colour like #a8c1bd.", nameof(Tiers));
            tier.Color = tier.Color!.ToLowerInvariant();
            if (tier.Weight is < 0 or > 1000)
                throw new ArgumentOutOfRangeException(nameof(Tiers), $"{label}: tier weights must be between 0 and 1000.");

            var id = Slug(string.IsNullOrWhiteSpace(tier.Id) ? tier.Name : tier.Id);
            var unique = id;
            for (var suffix = 2; !usedIds.Add(unique); suffix++) unique = $"{id}-{suffix}";
            tier.Id = unique;

            if (tier.Rewards.Count > MaximumRewardsPerTier)
                throw new ArgumentException($"{label}: tier {tier.Name} can have at most {MaximumRewardsPerTier} rewards.", nameof(Tiers));
            foreach (var reward in tier.Rewards)
            {
                if (Enabled && reward.PointTypeId <= 0)
                    throw new ArgumentOutOfRangeException(nameof(Tiers), $"{label}: select a point type for each reward in tier {tier.Name}.");
                if (reward.Minimum < 0 || reward.Maximum < reward.Minimum || reward.Maximum > StarshipExpeditionSettings.MaximumConfiguredReward)
                    throw new ArgumentOutOfRangeException(nameof(Tiers), $"{label}: reward ranges in tier {tier.Name} must be non-negative with the maximum at least the minimum.");
                if (reward.ChancePercent is < 1 or > 100)
                    throw new ArgumentOutOfRangeException(nameof(Tiers), $"{label}: reward chances in tier {tier.Name} must be between 1 and 100 percent.");
                reward.PointTypeName ??= string.Empty;
            }

            ValidateActions(tier.Actions, $"{label}: tier {tier.Name}");
        }

        ValidateActions(FailureActions, $"{label}: failure");
        if (Enabled && !Tiers.Any(tier => tier.Enabled && tier.Weight > 0))
            throw new ArgumentException($"{label}: at least one enabled tier needs a weight above zero.", nameof(Tiers));
    }

    public void ValidateCapsAndLoss(string label)
    {
        if (UsesLegacyFields) return;
        if (LossRules.Count > MaximumLossRules)
            throw new ArgumentException($"{label}: use at most {MaximumLossRules} loss rules.", nameof(LossRules));
        var lossTypes = new HashSet<int>();
        foreach (var rule in LossRules)
        {
            if (Enabled && rule.PointTypeId <= 0)
                throw new ArgumentOutOfRangeException(nameof(LossRules), $"{label}: select a point type for each loss rule.");
            if (!lossTypes.Add(rule.PointTypeId))
                throw new ArgumentException($"{label}: there are two loss rules for the same point type.", nameof(LossRules));
            if (rule.Percent is < 0 or > 100 || rule.MaximumPerEvent is < 0 or > StarshipExpeditionSettings.MaximumConfiguredReward)
                throw new ArgumentOutOfRangeException(nameof(LossRules), $"{label}: loss must be 0-100 percent with a non-negative amount cap.");
        }

        Caps = Caps.Where(cap => cap.PointTypeId > 0).GroupBy(cap => cap.PointTypeId).Select(group => group.First()).ToList();
        foreach (var cap in Caps)
        {
            if (cap.MaximumPerEvent is < 0 or > StarshipExpeditionSettings.MaximumConfiguredReward ||
                cap.MaximumUnbankedPerVoyage is < 0 or > StarshipExpeditionSettings.MaximumUnbankedReward)
                throw new ArgumentOutOfRangeException(nameof(Caps), $"{label}: point limits must be non-negative or unlimited.");
        }
    }

    private static void ValidateActions(List<StarshipRewardAction> actions, string label)
    {
        if (actions.Count > MaximumActionsPerList)
            throw new ArgumentException($"{label}: use at most {MaximumActionsPerList} actions.", nameof(actions));
        foreach (var action in actions)
        {
            if (action.ActionId <= 0)
                throw new ArgumentOutOfRangeException(nameof(actions), $"{label}: select an action for each action row.");
            if (!ActionTimings.Contains(action.Timing, StringComparer.Ordinal))
                throw new ArgumentException($"{label}: an action's timing must be Immediate or AfterBank.", nameof(actions));
            action.ActionName ??= string.Empty;
        }
    }

    private static string Slug(string value)
    {
        var slug = Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return slug.Length == 0 ? "tier" : slug;
    }
}
