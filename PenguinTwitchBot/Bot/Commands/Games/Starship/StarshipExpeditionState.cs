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
    public StarshipExpeditionEventSnapshot? CurrentEvent { get; set; }
}

public sealed class StarshipCrewMemberSnapshot
{
    public string UserId { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public DateTimeOffset JoinedAtUtc { get; init; }
    public StarshipCrewEnrollmentMethod EnrollmentMethod { get; init; }
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
    public string? ResolvedChoiceId { get; set; }
    public DateTimeOffset? ResolvedAtUtc { get; set; }
}

public sealed class StarshipEventChoiceSnapshot
{
    public string ChoiceId { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
}

public sealed class StarshipEventVoteSnapshot
{
    public string UserId { get; init; } = string.Empty;
    public string ChoiceId { get; init; } = string.Empty;
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
            snapshot.CurrentEvent == null ? null : StarshipOverlayEventSnapshot.From(snapshot.CurrentEvent),
            snapshot.CompletedEvents.LastOrDefault() is { } lastEvent ? StarshipOverlayEventResult.From(lastEvent) : null);
    }
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
    IReadOnlyDictionary<string, int> VoteCounts)
{
    public static StarshipOverlayEventSnapshot From(StarshipExpeditionEventSnapshot expeditionEvent) => new(
        expeditionEvent.Sequence,
        expeditionEvent.EventType,
        expeditionEvent.Title,
        expeditionEvent.Description,
        expeditionEvent.Sector,
        expeditionEvent.ClosesAtUtc,
        expeditionEvent.Choices.Select(choice => new StarshipOverlayChoice(choice.ChoiceId, choice.Label)).ToList(),
        expeditionEvent.DefaultChoiceId,
        expeditionEvent.Votes
            .GroupBy(vote => vote.ChoiceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase));
}

public sealed record StarshipOverlayChoice(string ChoiceId, string Label);

public sealed record StarshipOverlayEventResult(
    int Sequence,
    string Title,
    string Sector,
    string Choice,
    DateTimeOffset? ResolvedAtUtc)
{
    public static StarshipOverlayEventResult From(StarshipExpeditionEventSnapshot expeditionEvent)
    {
        var choice = expeditionEvent.Choices.FirstOrDefault(option =>
            string.Equals(option.ChoiceId, expeditionEvent.ResolvedChoiceId, StringComparison.OrdinalIgnoreCase));
        return new StarshipOverlayEventResult(
            expeditionEvent.Sequence,
            expeditionEvent.Title,
            expeditionEvent.Sector,
            choice?.Label ?? expeditionEvent.ResolvedChoiceId ?? string.Empty,
            expeditionEvent.ResolvedAtUtc);
    }
}