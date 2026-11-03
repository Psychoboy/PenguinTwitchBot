using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

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
    bool Required = true);

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
    bool AllowsAdditionalTriggers => false;
    string? RecommendedQueueName => null;
    ActionType Build(ActionRecipeContext context, IReadOnlyDictionary<string, string> parameters);
}