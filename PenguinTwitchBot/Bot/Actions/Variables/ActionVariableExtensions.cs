using PenguinTwitchBot.Database.Bot.Actions;

namespace PenguinTwitchBot.Bot.Actions.Variables;

/// <summary>
/// Extension methods for manipulating action variable collections consistently.
/// </summary>
public static class ActionVariableExtensions
{
    /// <summary>
    /// Adds or updates a variable in the dictionary, sanitizing token names and tracking precedence.
    /// </summary>
    public static void SetVariable(
        this Dictionary<string, ActionVariableInfo> variables,
        string name,
        string description,
        string source,
        string category,
        string? exampleValue = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        // Clean any leading/trailing % or whitespace for canonical dictionary key
        name = name.Trim().Trim('%');
        if (string.IsNullOrWhiteSpace(name)) return;

        var effectiveExample = exampleValue ?? (variables.TryGetValue(name, out var existing) ? existing.ExampleValue : null);

        variables[name] = new ActionVariableInfo(
            Name: name,
            Description: description,
            Source: source,
            Category: category,
            ExampleValue: effectiveExample);
    }
}

