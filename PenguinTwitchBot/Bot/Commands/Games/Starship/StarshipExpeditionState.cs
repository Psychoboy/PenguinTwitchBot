namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

public enum StarshipExpeditionStatus
{
    Cruising,
    EventOpen,
    Returned,
    Aborted
}

public enum StarshipCrewEnrollmentMethod
{
    ExplicitJoin,
    AutomaticAction
}

public sealed class StarshipExpeditionSnapshot
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public Guid ExpeditionId { get; init; }
    public int Seed { get; init; }
    public StarshipExpeditionStatus Status { get; set; } = StarshipExpeditionStatus.Cruising;
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int CurrentLeg { get; set; }
    public int LegCount { get; init; }
    public int NextEventSequence { get; set; } = 1;
    public DateTimeOffset? NextEventAtUtc { get; set; }
    public string CurrentSector { get; set; } = string.Empty;
    public string CurrentRoute { get; set; } = string.Empty;
    public List<StarshipCrewMemberSnapshot> Crew { get; init; } = [];
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
    public DateTimeOffset OpenedAtUtc { get; init; }
    public DateTimeOffset ClosesAtUtc { get; init; }
    public List<StarshipEventChoiceSnapshot> Choices { get; init; } = [];
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