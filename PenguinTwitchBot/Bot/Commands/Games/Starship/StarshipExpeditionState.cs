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
    public StarshipExpeditionEventSnapshot? CurrentEvent { get; set; }
}

public sealed class StarshipCrewMemberSnapshot
{
    public string UserId { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public DateTimeOffset JoinedAtUtc { get; init; }
    public StarshipCrewEnrollmentMethod EnrollmentMethod { get; init; }
    public List<StarshipUnbankedBalanceSnapshot> UnbankedBalances { get; init; } = [];
}

public sealed class StarshipExpeditionEventSnapshot
{
    public Guid EventId { get; init; }
    public int Sequence { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Sector { get; init; } = string.Empty;
    public DateTimeOffset OpenedAtUtc { get; init; }
    public DateTimeOffset ClosesAtUtc { get; init; }
    public List<StarshipEventChoiceSnapshot> Choices { get; init; } = [];
    public string DefaultChoiceId { get; init; } = string.Empty;
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

public sealed class StarshipRiskConfirmationSnapshot
{
    public string UserId { get; init; } = string.Empty;
    public string ChoiceId { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; init; }
    public int PointTypeId { get; init; }
    public string PointTypeName { get; init; } = string.Empty;
    public long ExposedBalance { get; init; }
    public long MaximumLoss { get; init; }
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
    StarshipOverlayEventResult? LatestResult)
{
    public static StarshipExpeditionOverlaySnapshot From(StarshipExpeditionSnapshot snapshot)
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
                    snapshot.RewardProfiles.FirstOrDefault(profile => profile.Enabled &&
                        string.Equals(profile.EventType, snapshot.CurrentEvent.EventType, StringComparison.OrdinalIgnoreCase))),
            GetLatestOverlayResult(snapshot));
    }

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
                .ToList());
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
    StarshipRewardDisclosureSnapshot? RewardDisclosure)
{
    public static StarshipOverlayEventSnapshot From(StarshipExpeditionEventSnapshot expeditionEvent, StarshipRewardProfile? profile) => new(
        expeditionEvent.Sequence,
        expeditionEvent.EventType,
        expeditionEvent.Title,
        expeditionEvent.Description,
        expeditionEvent.Sector,
        expeditionEvent.ClosesAtUtc,
        expeditionEvent.Choices.Select(choice => new StarshipOverlayChoice(choice.ChoiceId, choice.Label, choice.IsRisky)).ToList(),
        expeditionEvent.DefaultChoiceId,
        expeditionEvent.Votes
            .GroupBy(vote => vote.ChoiceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase),
        profile == null ? null : StarshipRewardDisclosureSnapshot.From(profile));
}

public sealed record StarshipOverlayChoice(string ChoiceId, string Label, bool IsRisky);

public sealed record StarshipRewardDisclosureSnapshot(
    string PointTypeName,
    int SuccessChancePercent,
    int CommonWeightPercent,
    long CommonMinimum,
    long CommonMaximum,
    int UncommonWeightPercent,
    long UncommonMinimum,
    long UncommonMaximum,
    int RareWeightPercent,
    long RareMinimum,
    long RareMaximum,
    int RiskLossPercent,
    long MaximumRiskLossPerEvent,
    long MaximumPerEvent,
    long MaximumUnbankedPerVoyage)
{
    public static StarshipRewardDisclosureSnapshot From(StarshipRewardProfile profile) => new(
        profile.PointTypeName,
        profile.SuccessChancePercent,
        profile.CommonWeightPercent,
        profile.CommonMinimum,
        profile.CommonMaximum,
        profile.UncommonWeightPercent,
        profile.UncommonMinimum,
        profile.UncommonMaximum,
        profile.RareWeightPercent,
        profile.RareMinimum,
        profile.RareMaximum,
        profile.RiskLossPercent,
        profile.MaximumRiskLossPerEvent,
        profile.MaximumPerEvent,
        profile.MaximumUnbankedPerVoyage);
}

public sealed record StarshipOverlayEventResult(
    int Sequence,
    string Title,
    string Sector,
    string Choice,
    DateTimeOffset? ResolvedAtUtc,
    IReadOnlyList<StarshipOverlayBankedAmount> BankedAmounts)
{
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
                .ToList());
    }
}

public sealed record StarshipOverlayBankedAmount(int PointTypeId, string PointTypeName, long Amount, bool IsPaid);