namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

public enum StarshipExpeditionStatus
{
    Cruising,
    EventOpen,
    Returned,
    Aborted,
    Boarding
}

public enum StarshipCrewEnrollmentMethod
{
    ExplicitJoin,
    AutomaticAction
}

public sealed class StarshipExpeditionSnapshot
{
    public const int CurrentSchemaVersion = 3;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public Guid ExpeditionId { get; init; }

    public int Seed { get; init; }
    public string StartedBy { get; init; } = string.Empty;
    public StarshipExpeditionStatus Status { get; set; } = StarshipExpeditionStatus.Cruising;
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int CurrentLeg { get; set; }
    public int LegCount { get; init; }
    public int StationIntervalLegs { get; init; } = 2;
    public int MinimumEventIntervalSeconds { get; init; } = 900;
    public int MaximumEventIntervalSeconds { get; init; } = 1800;
    public int EventVoteDurationSeconds { get; init; } = 60;
    public int BoardingDurationSeconds { get; init; } = 60;
    public DateTimeOffset? BoardingClosesAtUtc { get; set; }
    public bool ActiveCrewRewardsOnly { get; init; } = true;
    public List<StarshipRewardProfile> RewardProfiles { get; init; } = [];
    public Dictionary<string, string> EventRewardProfileIds { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<StarshipEventDefinition>? EventCatalog { get; init; }
    public bool AnnounceSessionStatusInChat { get; init; } = true;
    public string SessionStartChatMessageTemplate { get; init; } = string.Empty;
    public string SessionEndChatMessageTemplate { get; init; } = string.Empty;
    public int NextEventSequence { get; set; } = 1;
    public DateTimeOffset? NextEventAtUtc { get; set; }
    public string CurrentSector { get; set; } = string.Empty;
    public string CurrentRoute { get; set; } = string.Empty;
    public List<string> SectorPool { get; init; } = [];
    public List<StarshipCrewMemberSnapshot> Crew { get; init; } = [];
    public List<StarshipExpeditionEventSnapshot> CompletedEvents { get; init; } = [];
    public List<StarshipBankedPayoutSnapshot> BankedPayouts { get; init; } = [];
    /// <summary>Reward-profile actions waiting to run, or already run (kept so each fires exactly once).</summary>
    public List<StarshipPendingActionSnapshot> PendingActions { get; init; } = [];
    public StarshipExpeditionEventSnapshot? CurrentEvent { get; set; }
    /// <summary>While in the future, the status widget stays visible even when it would otherwise fade out during cruising.</summary>
    public DateTimeOffset? StatusForcedUntilUtc { get; set; }
    public List<StarshipActivityItem> ActivityFeed { get; init; } = [];
    public long NextActivityId { get; set; } = 1;
    public StarshipShipSnapshot Ship { get; init; } = new();
    public StarshipShipSystemsSnapshot? ShipSystems { get; init; }
    public string ReturnReason { get; set; } = string.Empty;
}

/// <summary>Look of the voyage's ship, generated once from the voyage seed.</summary>
public sealed class StarshipShipSnapshot
{
    public string Name { get; init; } = string.Empty;
    public string HullStyle { get; init; } = string.Empty;
    public string AccentColor { get; init; } = string.Empty;
    /// <summary>Large stable number used to pick among custom ship images.</summary>
    public int Variant { get; init; }
}

public static class StarshipActivityKinds
{
    public const string CrewJoined = "CrewJoined";
    public const string EventOpened = "EventOpened";
    public const string StepAdvanced = "StepAdvanced";
    public const string RarityRevealed = "RarityRevealed";
    public const string EventResolved = "EventResolved";
    public const string VoyageEnded = "VoyageEnded";
}

/// <summary>Short-lived happening that overlays animate once; ids increase for the life of a voyage.</summary>
public sealed class StarshipActivityItem
{
    public long Id { get; init; }
    public DateTimeOffset AtUtc { get; init; }
    public string Kind { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public string Actor { get; init; } = string.Empty;
}

public sealed class StarshipCrewMemberSnapshot
{
    public string UserId { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public DateTimeOffset JoinedAtUtc { get; init; }
    public StarshipCrewEnrollmentMethod EnrollmentMethod { get; init; }
    /// <summary>Events with a lower sequence do not reward or penalize this member (joined while they were open).</summary>
    public int EffectiveFromSequence { get; init; }
    public List<StarshipUnbankedBalanceSnapshot> UnbankedBalances { get; init; } = [];
}

public sealed class StarshipExpeditionEventSnapshot
{
    public Guid EventId { get; init; }
    public int Sequence { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    /// <summary>Kind of object the overlay draws (wreck, beacon, debris, station, fork) and which of its looks.</summary>
    public string ObjectKind { get; init; } = string.Empty;
    public List<string> ObjectImages { get; init; } = [];
    public Dictionary<string, string>? LootByTier { get; init; }
    public int ObjectVariant { get; init; }
    /// <summary>Large stable number used to pick among custom object images.</summary>
    public int ObjectRoll { get; init; }
    public string SuccessStory { get; init; } = string.Empty;
    public string FailureStory { get; init; } = string.Empty;
    public string OutcomeStory { get; set; } = string.Empty;
    public string Sector { get; init; } = string.Empty;
    public DateTimeOffset OpenedAtUtc { get; set; }
    public DateTimeOffset ClosesAtUtc { get; set; }
    public List<StarshipEventChoiceSnapshot> Choices { get; set; } = [];
    /// <summary>Every choice this event can offer; <see cref="Choices"/> is the subset available at the current step.</summary>
    public List<StarshipEventChoiceSnapshot> ChoicePool { get; set; } = [];
    public string DefaultChoiceId { get; init; } = string.Empty;
    public int StepNumber { get; set; } = 1;
    public List<string> Flags { get; init; } = [];
    public List<StarshipEventPathStepSnapshot> Path { get; init; } = [];
    public int SuccessChanceModifierPercent { get; set; }
    public int RewardMultiplierPercent { get; set; } = 100;
    public int RiskMultiplierPercent { get; set; } = 100;
    public string? RevealedRarity { get; set; }
    public string RevealedRarityColor { get; set; } = string.Empty;
    public string StepNote { get; set; } = string.Empty;
    public string OutcomeResult { get; set; } = string.Empty;
    public string OutcomeRarity { get; set; } = string.Empty;
    public string OutcomeRarityColor { get; set; } = string.Empty;
    public StarshipShipEventContext? ShipContext { get; set; }
    public List<StarshipEventVoteSnapshot> Votes { get; init; } = [];
    public List<string> EligibleRewardUserIds { get; init; } = [];
    public List<StarshipRewardOutcomeSnapshot> RewardOutcomes { get; init; } = [];
    public List<StarshipRiskConfirmationSnapshot> PendingRiskConfirmations { get; init; } = [];
    public string? ResolvedChoiceId { get; set; }
    public DateTimeOffset? ResolvedAtUtc { get; set; }
}

public sealed class StarshipEventChoiceSnapshot
{
    public string ChoiceId { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public bool IsRisky { get; init; }
    /// <summary>Story template shown when this choice is made (see StarshipExpeditionStory for tokens).</summary>
    public string Story { get; init; } = string.Empty;
    /// <summary>When true, winning this choice ends the event with no reward for anyone.</summary>
    public bool EndsWithoutReward { get; init; }
    /// <summary>When true, winning this choice advances the event to another step instead of resolving it.</summary>
    public bool ContinuesEvent { get; init; }
    public string GrantsFlag { get; init; } = string.Empty;
    public bool RevealsRarity { get; init; }
    public int SuccessChanceModifierPercent { get; init; }
    public int RewardMultiplierPercent { get; init; } = 100;
    public int RiskMultiplierPercent { get; init; } = 100;
    public string Hint { get; init; } = string.Empty;
    public StarshipShipEffect? ShipEffect { get; init; }
}

public sealed class StarshipEventPathStepSnapshot
{
    public int StepNumber { get; init; }
    public string ChoiceId { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public int VoteCount { get; init; }
}

public sealed class StarshipEventVoteSnapshot
{
    public string UserId { get; init; } = string.Empty;
    public string ChoiceId { get; init; } = string.Empty;
}

public sealed class StarshipUnbankedBalanceSnapshot
{
    public int PointTypeId { get; init; }
    public string PointTypeName { get; init; } = string.Empty;
    public long Amount { get; set; }
}

public sealed class StarshipRewardOutcomeSnapshot
{
    public string UserId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int PointTypeId { get; init; }
    public string PointTypeName { get; init; } = string.Empty;
    public string Outcome { get; init; } = string.Empty;
    public string Rarity { get; init; } = string.Empty;
    public bool WasRiskyAction { get; init; }
    public int SuccessChancePercent { get; init; }
    public long EarnedAmount { get; init; }
    public long LostAmount { get; init; }
}

public sealed class StarshipBankedPayoutSnapshot
{
    public string OperationId { get; init; } = string.Empty;
    public Guid EventId { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int PointTypeId { get; init; }
    public string PointTypeName { get; init; } = string.Empty;
    public long Amount { get; init; }
    public bool IsPaid { get; set; }
    public DateTimeOffset? PaidAtUtc { get; set; }
}

public sealed class StarshipPendingActionSnapshot
{
    public string OperationId { get; init; } = string.Empty;
    public int ActionId { get; init; }
    public string ActionName { get; init; } = string.Empty;
    public string Timing { get; init; } = StarshipActionTimings.Immediate;
    public Guid EventId { get; init; }
    /// <summary>For AfterBank actions: the bank (station, return or abort) whose payouts must settle first.</summary>
    public Guid? BankEventId { get; set; }
    public bool Executed { get; set; }
    public int Attempts { get; set; }
    public Dictionary<string, string> Variables { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class StarshipRiskConfirmationSnapshot
{
    public string UserId { get; init; } = string.Empty;
    public string ChoiceId { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; init; }
    public int PointTypeId { get; init; }
    public string PointTypeName { get; init; } = string.Empty;
    public long ExposedBalance { get; init; }
    public long MaximumLoss { get; init; }
    public int SuccessChancePercent { get; init; }
    public int RewardMultiplierPercent { get; init; } = 100;
    public string? RevealedRarity { get; init; }
}

public sealed record StarshipExpeditionOverlaySnapshot(
    string Status,
    DateTimeOffset UpdatedAtUtc,
    int CurrentLeg,
    int LegCount,
    string CurrentSector,
    string CurrentRoute,
    int CrewCount,
    DateTimeOffset? BoardingClosesAtUtc,
    DateTimeOffset? NextEventAtUtc,
    StarshipOverlayEventSnapshot? CurrentEvent,
    StarshipOverlayEventResult? LatestResult,
    DateTimeOffset? StatusForcedUntilUtc,
    DateTimeOffset ServerNowUtc,
    IReadOnlyList<StarshipActivityItem> Activity,
    StarshipShipSnapshot Ship,
    IReadOnlyList<int> StationLegs,
    IReadOnlyList<StarshipOverlayTimelineEntry> Timeline)
{
    public Guid ExpeditionId { get; init; }
    public StarshipOverlayShipState? ShipSystems { get; init; }

    public static StarshipExpeditionOverlaySnapshot From(StarshipExpeditionSnapshot snapshot, DateTimeOffset? serverNowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new StarshipExpeditionOverlaySnapshot(
            snapshot.Status.ToString(),
            snapshot.UpdatedAtUtc,
            snapshot.CurrentLeg,
            snapshot.LegCount,
            snapshot.CurrentSector,
            snapshot.CurrentRoute,
            snapshot.Crew.Count,
            snapshot.BoardingClosesAtUtc,
            snapshot.NextEventAtUtc,
            snapshot.CurrentEvent == null
                ? null
                : StarshipOverlayEventSnapshot.From(
                    snapshot.CurrentEvent,
                    StarshipRewardProfile.FindForEvent(snapshot.RewardProfiles, snapshot.EventRewardProfileIds, snapshot.CurrentEvent.EventType), snapshot),
            GetLatestOverlayResult(snapshot),
            snapshot.StatusForcedUntilUtc,
            serverNowUtc ?? DateTimeOffset.UtcNow,
            snapshot.ActivityFeed.TakeLast(MaximumActivityItems).ToList(),
            snapshot.Ship,
            GetStationLegs(snapshot),
            snapshot.CompletedEvents.Select(item => new StarshipOverlayTimelineEntry(
                item.Sequence, item.Title, item.OutcomeResult, item.OutcomeRarity, item.ObjectKind)).ToList())
        {
            ExpeditionId = snapshot.ExpeditionId,
            ShipSystems = snapshot.ShipSystems is { IntegrationEnabled: true } ship
                ? new(ship.Hull, ship.Supplies, ship.PendingSuccessBonusPercent, ship.ActiveSuccessBonusPercent, snapshot.ReturnReason) : null
        };
    }

    /// <summary>1-based legs that are station stops (leg 1 is always the route choice).</summary>
    public static IReadOnlyList<int> GetStationLegs(StarshipExpeditionSnapshot snapshot) =>
        snapshot.StationIntervalLegs <= 0
            ? []
            : Enumerable.Range(2, Math.Max(0, snapshot.LegCount - 1))
                .Where(leg => (leg - 1) % snapshot.StationIntervalLegs == 0)
                .ToList();

    public const int MaximumActivityItems = 20;

    private static StarshipOverlayEventResult? GetLatestOverlayResult(StarshipExpeditionSnapshot snapshot)
    {
        var lastEvent = snapshot.CompletedEvents.LastOrDefault();
        if (lastEvent != null)
        {
            var payouts = snapshot.BankedPayouts.Where(payout => payout.EventId == lastEvent.EventId ||
                IsTerminal(snapshot.Status) && payout.EventId == snapshot.ExpeditionId);
            return StarshipOverlayEventResult.From(lastEvent, payouts);
        }

        if (!IsTerminal(snapshot.Status)) return null;
        return new StarshipOverlayEventResult(
            snapshot.CurrentLeg,
            "Starship Expedition",
            snapshot.CurrentSector,
            snapshot.Status.ToString(),
            snapshot.UpdatedAtUtc,
            snapshot.BankedPayouts.Where(payout => payout.EventId == snapshot.ExpeditionId)
                .GroupBy(payout => new { payout.PointTypeId, payout.PointTypeName })
                .Select(group => new StarshipOverlayBankedAmount(
                    group.Key.PointTypeId, group.Key.PointTypeName, group.Sum(payout => payout.Amount), group.All(payout => payout.IsPaid)))
                .ToList(),
            snapshot.Status.ToString(),
            string.Empty,
            StarshipExpeditionNarrative.BuildVoyageLines(snapshot),
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0);
    }

    private static bool IsTerminal(StarshipExpeditionStatus status) =>
        status is StarshipExpeditionStatus.Returned or StarshipExpeditionStatus.Aborted;
}

public sealed record StarshipOverlayEventSnapshot(
    int Sequence,
    string EventType,
    string Title,
    string Description,
    string Sector,
    DateTimeOffset ClosesAtUtc,
    IReadOnlyList<StarshipOverlayChoice> Choices,
    string DefaultChoiceId,
    IReadOnlyDictionary<string, int> VoteCounts,
    StarshipRewardDisclosureSnapshot? RewardDisclosure,
    int StepNumber,
    IReadOnlyList<string> Path,
    string? RevealedRarity,
    string StepNote,
    int? EffectiveSuccessChancePercent,
    int RewardMultiplierPercent,
    int RiskMultiplierPercent,
    string Subject,
    string ObjectKind,
    int ObjectVariant,
    DateTimeOffset OpenedAtUtc,
    int ObjectRoll,
    string RevealedRarityColor = "")
{
    public IReadOnlyList<string> ObjectImages { get; init; } = [];
    public StarshipShipEventDisclosure? ShipDisclosure { get; init; }

    public static StarshipOverlayEventSnapshot From(StarshipExpeditionEventSnapshot expeditionEvent, StarshipRewardProfile? profile,
        StarshipExpeditionSnapshot? voyage = null) => new(
        expeditionEvent.Sequence,
        expeditionEvent.EventType,
        expeditionEvent.Title,
        expeditionEvent.Description,
        expeditionEvent.Sector,
        expeditionEvent.ClosesAtUtc,
        expeditionEvent.Choices.Select((choice, index) => new StarshipOverlayChoice(
            choice.ChoiceId, choice.Label, choice.IsRisky, index + 1, StarshipExpeditionNarrative.DescribeEffects(choice),
            choice.SuccessChanceModifierPercent, choice.RewardMultiplierPercent, choice.RiskMultiplierPercent,
            choice.ContinuesEvent, choice.RevealsRarity, choice.EndsWithoutReward)).ToList(),
        expeditionEvent.DefaultChoiceId,
        expeditionEvent.Votes
            .GroupBy(vote => vote.ChoiceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase),
        profile == null ? null : StarshipRewardDisclosureSnapshot.From(profile),
        expeditionEvent.StepNumber,
        expeditionEvent.Path.Select(step => step.Label).ToList(),
        expeditionEvent.RevealedRarity,
        expeditionEvent.StepNote,
        profile == null && (expeditionEvent.ShipContext == null || StarshipEventCatalog.IsStructural(expeditionEvent.EventType))
            ? null
            : StarshipRewardResolver.GetEffectiveSuccessChance(profile, expeditionEvent),
        expeditionEvent.RewardMultiplierPercent,
        expeditionEvent.RiskMultiplierPercent,
        expeditionEvent.Subject,
        expeditionEvent.ObjectKind,
        expeditionEvent.ObjectVariant,
        expeditionEvent.OpenedAtUtc,
        expeditionEvent.ObjectRoll,
        expeditionEvent.RevealedRarityColor)
        {
            ObjectImages = expeditionEvent.ObjectImages,
            ShipDisclosure = voyage == null ? null : StarshipShipEventDisclosure.From(voyage, expeditionEvent, profile)
        };
}

public sealed record StarshipOverlayChoice(
    string ChoiceId,
    string Label,
    bool IsRisky,
    int Number,
    string Hint,
    int SuccessModifierPercent = 0,
    int RewardMultiplierPercent = 100,
    int RiskMultiplierPercent = 100,
    bool ContinuesEvent = false,
    bool RevealsRarity = false,
    bool EndsWithoutReward = false);

public sealed record StarshipOverlayTimelineEntry(int Sequence, string Title, string Outcome, string Rarity, string ObjectKind);

public sealed record StarshipRewardDisclosureTier(
    string Name,
    string Color,
    int ChancePercent,
    string RewardText,
    bool RunsActions);

public sealed record StarshipRewardDisclosureSnapshot(
    string PointTypeName,
    int SuccessChancePercent,
    IReadOnlyList<StarshipRewardDisclosureTier> Tiers,
    int RiskLossPercent,
    long? MaximumRiskLossPerEvent,
    long? MaximumPerEvent,
    long? MaximumUnbankedPerVoyage)
{
    public IReadOnlyList<StarshipRewardCurrencyDisclosure> Currencies { get; init; } = [];

    public static StarshipRewardDisclosureSnapshot From(StarshipRewardProfile profile)
    {
        var primaryType = profile.GetPrimaryPointTypeId();
        var loss = profile.GetLossRules().FirstOrDefault(rule => rule.PointTypeId == primaryType) ?? profile.GetLossRules().FirstOrDefault();
        var cap = profile.GetCap(primaryType);
        return new StarshipRewardDisclosureSnapshot(
            profile.GetPointTypeName(primaryType),
            profile.SuccessChancePercent,
            StarshipRewardTiers.DescribeTiers(profile),
            loss?.Percent ?? 0,
            loss?.MaximumPerEvent,
            cap.MaximumPerEvent,
            cap.MaximumUnbankedPerVoyage)
        {
            Currencies = profile.GetPointTypeIds().Select(id =>
            {
                var currencyCap = profile.GetCap(id);
                var lossRule = profile.GetLossRules().FirstOrDefault(rule => rule.PointTypeId == id);
                return new StarshipRewardCurrencyDisclosure(profile.GetPointTypeName(id), currencyCap.MaximumPerEvent,
                    currencyCap.MaximumUnbankedPerVoyage, lossRule?.Percent ?? 0, lossRule?.MaximumPerEvent);
            }).ToList()
        };
    }
}

public sealed record StarshipRewardCurrencyDisclosure(string PointTypeName, long? MaximumPerEvent,
    long? MaximumUnbankedPerVoyage, int RiskLossPercent, long? MaximumRiskLossPerEvent);

public static class StarshipRewardTiers
{
    /// <summary>Enabled tiers with their chance as a whole percent (largest remainder, so the total is 100).</summary>
    public static IReadOnlyList<StarshipRewardDisclosureTier> DescribeTiers(StarshipRewardProfile profile)
    {
        var tiers = profile.GetTiers().Where(tier => tier.Enabled && tier.Weight > 0).ToList();
        var total = tiers.Sum(tier => tier.Weight);
        if (total <= 0) return [];
        var shares = tiers.Select(tier => tier.Weight * 100.0 / total).ToList();
        var percents = shares.Select(share => (int)Math.Floor(share)).ToList();
        var remainder = 100 - percents.Sum();
        foreach (var index in Enumerable.Range(0, tiers.Count).OrderByDescending(index => shares[index] - percents[index]).Take(remainder))
            percents[index]++;
        return tiers.Select((tier, index) => new StarshipRewardDisclosureTier(
            tier.Name, tier.Color, percents[index], DescribeRewards(profile, tier), tier.Actions.Count > 0)).ToList();
    }

    public static string DescribeRewards(StarshipRewardProfile profile, StarshipRewardTier tier)
    {
        var parts = tier.Rewards.Select(reward =>
        {
            var name = string.IsNullOrWhiteSpace(reward.PointTypeName) ? profile.GetPointTypeName(reward.PointTypeId) : reward.PointTypeName;
            var range = reward.Minimum == reward.Maximum ? $"{reward.Minimum}" : $"{reward.Minimum}-{reward.Maximum}";
            var chance = reward.ChancePercent < 100 ? $" ({reward.ChancePercent}% chance)" : string.Empty;
            return $"{range} {name}{chance}".Trim();
        }).ToList();
        return parts.Count == 0 ? "no points" : string.Join(" + ", parts);
    }
}

public sealed record StarshipOverlayEventResult(
    int Sequence,
    string Title,
    string Sector,
    string Choice,
    DateTimeOffset? ResolvedAtUtc,
    IReadOnlyList<StarshipOverlayBankedAmount> BankedAmounts,
    string Outcome,
    string Rarity,
    IReadOnlyList<string> Lines,
    string Story,
    string Subject,
    string ObjectKind,
    int ObjectVariant,
    int ObjectRoll,
    string RarityColor = "")
{
    public IReadOnlyList<string> ObjectImages { get; init; } = [];

    public static StarshipOverlayEventResult From(
        StarshipExpeditionEventSnapshot expeditionEvent,
        IEnumerable<StarshipBankedPayoutSnapshot> payouts)
    {
        var choice = expeditionEvent.Choices.FirstOrDefault(option =>
            string.Equals(option.ChoiceId, expeditionEvent.ResolvedChoiceId, StringComparison.OrdinalIgnoreCase));
        return new StarshipOverlayEventResult(
            expeditionEvent.Sequence,
            expeditionEvent.Title,
            expeditionEvent.Sector,
            choice?.Label ?? expeditionEvent.ResolvedChoiceId ?? string.Empty,
            expeditionEvent.ResolvedAtUtc,
            payouts.GroupBy(payout => new { payout.PointTypeId, payout.PointTypeName })
                .Select(group => new StarshipOverlayBankedAmount(
                    group.Key.PointTypeId,
                    group.Key.PointTypeName,
                    group.Sum(payout => payout.Amount),
                    group.All(payout => payout.IsPaid)))
                .ToList(),
            expeditionEvent.OutcomeResult,
            expeditionEvent.OutcomeRarity,
            StarshipExpeditionNarrative.BuildEventLines(expeditionEvent, includeStory: false),
            expeditionEvent.OutcomeStory,
            expeditionEvent.Subject,
            expeditionEvent.ObjectKind,
            expeditionEvent.ObjectVariant,
            expeditionEvent.ObjectRoll,
            expeditionEvent.OutcomeRarityColor)
            { ObjectImages = expeditionEvent.ObjectImages };
    }
}

public sealed record StarshipOverlayBankedAmount(int PointTypeId, string PointTypeName, long Amount, bool IsPaid);