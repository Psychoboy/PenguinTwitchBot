using System.Globalization;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Database.Bot.Models.Timers;

namespace PenguinTwitchBot.Bot.Actions.Recipes;

public sealed class TimedAnnouncementRecipe : ITimerGroupActionRecipe
{
    private static readonly IReadOnlyList<ActionRecipeParameter> RecipeParameters = CreateParameters();

    public string Id => "timed-announcements";
    public string Title => "Timed announcements";
    public string Category => "Chat";
    public string Icon => "mdi-message-badge-outline";
    public string Description => "Rotate through announcement messages on a timer, with optional minimum chat activity.";
    public IReadOnlyList<ActionRecipeParameter> Parameters => RecipeParameters;
    public bool RequiresCommand => false;
    public bool SupportsVariables => true;
    public string? SuggestedGroupName => "Recipes / Timed Announcements";
    public IReadOnlyList<string> VariableParameterKeys => ["Message1", "Message2", "Message3", "Message4", "Message5"];

    public TimerGroup BuildTimerGroup(IReadOnlyDictionary<string, string> parameters)
    {
        var timerName = GetParameter(parameters, "TimerName").Trim();
        if (string.IsNullOrWhiteSpace(timerName))
            throw new ArgumentException("Timer name is required.", nameof(parameters));
        if (!int.TryParse(GetParameter(parameters, "IntervalSeconds"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var interval) || interval is < 30 or > 86400)
            throw new ArgumentException("Interval must be between 30 and 86400 seconds.", nameof(parameters));
        if (!int.TryParse(GetParameter(parameters, "MinimumMessages"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var minimumMessages) || minimumMessages is < 0 or > 10000)
            throw new ArgumentException("Minimum chat messages must be between 0 and 10000.", nameof(parameters));

        var now = DateTime.UtcNow;
        return new TimerGroup
        {
            Id = Guid.CreateVersion7(),
            Name = timerName,
            Active = true,
            Repeat = true,
            OnlineOnly = true,
            IntervalMinimumSeconds = interval,
            IntervalMaximumSeconds = interval,
            MinimumMessages = minimumMessages,
            Shuffle = false,
            LastRun = now,
            NextRun = now.AddSeconds(interval)
        };
    }

    public IReadOnlyList<ActionType> BuildTimerActions(
        ActionRecipeContext context,
        TimerGroup timerGroup,
        IReadOnlyDictionary<string, string> parameters)
    {
        var messages = Enumerable.Range(1, 5)
            .Select(index => GetParameter(parameters, $"Message{index}").Trim())
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToList();
        if (messages.Count == 0)
            throw new ArgumentException("Enter at least one announcement message.", nameof(parameters));

        return messages.Select((message, index) => new ActionType
        {
            Name = $"{context.ActionName.Trim()} - {index + 1}",
            Group = "Recipes / Timed Announcements",
            QueueName = context.QueueName,
            SubActions = [new SendMessageType { Index = 0, Text = message, Enabled = true }],
            Triggers =
            [
                new TriggerType
                {
                    Name = timerGroup.Name,
                    Type = TriggerTypes.Timer,
                    TimerGroupId = timerGroup.Id,
                    Enabled = true
                }
            ]
        }).ToList();
    }

    public ActionType Build(ActionRecipeContext context, IReadOnlyDictionary<string, string> parameters)
    {
        var timerGroup = BuildTimerGroup(parameters);
        return BuildTimerActions(context, timerGroup, parameters).First();
    }

    private static IReadOnlyList<ActionRecipeParameter> CreateParameters()
    {
        var parameters = new List<ActionRecipeParameter>
        {
            new("TimerName", "Timer name", "Unique name for this timer group."),
            new("IntervalSeconds", "Interval (seconds)", "Run between announcements every 30 to 86400 seconds.", DefaultValue: "300"),
            new("MinimumMessages", "Minimum chat messages", "Require this many chat messages since the previous timer run.", DefaultValue: "15")
        };
        for (var index = 1; index <= 5; index++)
        {
            parameters.Add(new ActionRecipeParameter(
                $"Message{index}",
                $"Announcement {index}",
                index == 1 ? "At least one message is required." : "Leave blank to omit this announcement.",
                ActionRecipeParameterType.Multiline,
                Required: index == 1,
                ReplaceDefaultWhenInsertingVariable: true));
        }
        return parameters;
    }

    private static string GetParameter(IReadOnlyDictionary<string, string> parameters, string key) =>
        parameters.GetValueOrDefault(key, string.Empty);
}