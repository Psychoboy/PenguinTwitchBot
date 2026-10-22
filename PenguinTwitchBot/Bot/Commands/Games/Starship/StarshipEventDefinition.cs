using System.Text.Json.Serialization;
using PenguinTwitchBot.Bot.Commands.Games;

namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

public sealed record StarshipEventDefinition(
    string EventType,
    string Title,
    string Description,
    List<StarshipEventChoiceSnapshot> Choices,
    string DefaultChoiceId,
    string SuccessStory = "",
    string FailureStory = "")
{
    public string StartStepId { get; init; } = string.Empty;
    public int DefaultStepSuppliesCost { get; init; }
    public List<StarshipEventStepDefinition> Steps { get; init; } = [];
    public bool Enabled { get; init; } = true;
    public int Weight { get; init; } = 1;
    public int SortOrder { get; init; }
    public List<string> AllowedRoutes { get; init; } = [];
    public List<string> AllowedSectors { get; init; } = [];
    public string SubjectStyle { get; init; } = string.Empty;
    public List<string> SubjectNames { get; init; } = [];
    public string ObjectKind { get; init; } = string.Empty;
    public List<string> Images { get; init; } = [];
    public Dictionary<string, string> LootByTier { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string? RewardProfileId { get; init; }
    public int? SuccessChancePercent { get; init; }
    public StarshipShipEffect? ShipSuccessEffect { get; init; }
    public StarshipShipEffect? ShipFailureEffect { get; init; }
    public DropGameSoundOverride OpenSound { get; init; } = new();
}

[JsonConverter(typeof(JsonStringEnumConverter<StarshipEventStepType>))]
public enum StarshipEventStepType
{
    Vote,
    Automatic,
    End
}

public sealed class StarshipEventStepDefinition
{
    public string StepId { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public StarshipEventStepType Type { get; set; } = StarshipEventStepType.Vote;
    public int? SuppliesCostOverride { get; set; }
    public string? Narrative { get; set; }
    public List<StarshipEventChoiceSnapshot> Choices { get; set; } = [];
    public string DefaultChoiceId { get; set; } = string.Empty;
    public string? NextStepId { get; set; }
}