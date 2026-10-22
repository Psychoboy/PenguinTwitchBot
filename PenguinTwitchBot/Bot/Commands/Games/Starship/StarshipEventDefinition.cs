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