using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Database.Bot.Models.Timers;

namespace PenguinTwitchBot.Bot.Actions.Recipes;

public enum ActionRecipeParameterType
{
    Text,
    Multiline
}

public sealed record ActionRecipeParameter(
    string Key,
    string Label,
    string HelperText,
    ActionRecipeParameterType Type = ActionRecipeParameterType.Text,
    string DefaultValue = "",
    bool Required = true,
    bool ReplaceDefaultWhenInsertingVariable = false)
{
    public string InsertVariableToken(string currentValue, string token) =>
        ReplaceDefaultWhenInsertingVariable && currentValue.Equals(DefaultValue, StringComparison.Ordinal)
            ? token
            : currentValue + token;
}

public sealed record ActionRecipeContext(
    string ActionName,
    string QueueName,
    string CommandName,
    Guid CommandId);

public interface IActionRecipe
{
    string Id { get; }
    string Title { get; }
    string Category { get; }
    string Icon { get; }
    string Description { get; }
    IReadOnlyList<ActionRecipeParameter> Parameters { get; }
    bool RequiresCommand => true;
    bool SupportsCommandSettings => false;
    bool SupportsVariables => false;
    bool AllowsAdditionalTriggers => false;
    bool RequiresTrigger => false;
    string TriggerRequirementDescription => "supported trigger";
    string TriggerButtonLabel => "Add trigger";
    IReadOnlyCollection<TriggerTypes>? AllowedTriggerTypes => null;
    IReadOnlyCollection<string>? AllowedTwitchEventNames => null;
    bool IsRequiredTrigger(TriggerType trigger) => true;
    string? RecommendedQueueName => null;
    string? SuggestedGroupName => null;
    IReadOnlyList<string> VariableParameterKeys => [];
    ActionType Build(ActionRecipeContext context, IReadOnlyDictionary<string, string> parameters);
}

public interface ITimerGroupActionRecipe : IActionRecipe
{
    TimerGroup BuildTimerGroup(IReadOnlyDictionary<string, string> parameters);
    IReadOnlyList<ActionType> BuildTimerActions(ActionRecipeContext context, TimerGroup timerGroup, IReadOnlyDictionary<string, string> parameters);
}

public interface IRaffleSetupActionRecipe : IActionRecipe
{
    PenguinTwitchBot.Bot.Actions.RaffleSetupRequest BuildRequest(
        string groupName,
        string queueName,
        IReadOnlyDictionary<string, string> parameters);
}