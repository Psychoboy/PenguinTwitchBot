using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Starship;

namespace PenguinTwitchBot.Pages.Games;

public sealed class StarshipEventDraft
{
    public string EventType { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public List<StarshipChoiceDraft> Choices { get; set; } = [];
    public string StartStepId { get; set; } = "";
    public int DefaultStepSuppliesCost { get; set; }
    public List<StarshipEventStepDraft> Steps { get; set; } = [];
    public string DefaultChoiceId { get; set; } = "";
    public string SuccessStory { get; set; } = "";
    public string FailureStory { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public int Weight { get; set; } = 1;
    public int SortOrder { get; set; }
    public List<string> AllowedRoutes { get; set; } = [];
    public List<string> AllowedSectors { get; set; } = [];
    public string SubjectStyle { get; set; } = "";
    public List<string> SubjectNames { get; set; } = [];
    public string ObjectKind { get; set; } = "";
    public List<string> Images { get; set; } = [];
    public Dictionary<string, string> LootByTier { get; set; } = [];
    public string? RewardProfileId { get; set; }
    public int? SuccessChancePercent { get; set; }
    public StarshipShipEffect? ShipSuccessEffect { get; set; }
    public StarshipShipEffect? ShipFailureEffect { get; set; }
    public DropGameSoundOverride OpenSound { get; set; } = new();

    public StarshipEventDefinition ToDefinition() => StarshipEventEditing.Convert<StarshipEventDefinition>(this);
    public static StarshipEventDraft From(StarshipEventDefinition definition) => StarshipEventEditing.Convert<StarshipEventDraft>(definition);
}

public sealed class StarshipChoiceDraft
{
    public string ChoiceId { get; set; } = "";
    public string? NextStepId { get; set; }
    public string Label { get; set; } = "";
    public bool IsRisky { get; set; }
    public string Story { get; set; } = "";
    public bool EndsWithoutReward { get; set; }
    public bool ContinuesEvent { get; set; }
    public string GrantsFlag { get; set; } = "";
    public bool RevealsRarity { get; set; }
    public int SuccessChanceModifierPercent { get; set; }
    public int RewardMultiplierPercent { get; set; } = 100;
    public int RiskMultiplierPercent { get; set; } = 100;
    public string Hint { get; set; } = "";
    public StarshipShipEffect? ShipEffect { get; set; }
}

public sealed class StarshipEventStepDraft
{
    public string StepId { get; set; } = "";
    public string Prompt { get; set; } = "";
    public StarshipEventStepType Type { get; set; }
    public string? Narrative { get; set; }
    public int? SuppliesCostOverride { get; set; }
    public List<StarshipChoiceDraft> Choices { get; set; } = [];
    public string DefaultChoiceId { get; set; } = "";
    public string? NextStepId { get; set; }
}