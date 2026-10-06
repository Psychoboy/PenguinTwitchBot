namespace PenguinTwitchBot.Bot.Actions.Variables;

/// <summary>
/// Metadata describing an available template variable for an action.
/// </summary>
/// <param name="Name">Variable token name without percent signs (e.g. "User", "streamer").</param>
/// <param name="Description">Description of what the variable contains.</param>
/// <param name="Source">Where the variable originates (e.g. "Trigger: Command (!points)", "Step 2: Set Variable").</param>
/// <param name="Category">Categorization grouping (Globals, Triggers, Previous Steps, Caller Actions, Special).</param>
/// <param name="ExampleValue">Optional example value for display.</param>
public record ActionVariableInfo(
    string Name,
    string Description,
    string Source,
    string Category,
    string? ExampleValue = null);
