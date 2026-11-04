using System.Globalization;
using PenguinTwitchBot.Database.Bot.Actions;

namespace PenguinTwitchBot.Bot.Actions.Recipes;

public sealed class TimedRaffleRecipe : IRaffleSetupActionRecipe
{
    private static readonly IReadOnlyList<ActionRecipeParameter> RecipeParameters =
    [
        new("RaffleName", "Raffle name", "Name shown to viewers.", DefaultValue: "Timed Raffle"),
        new("RaffleKey", "Raffle key", "Unique key used internally by raffle actions.", DefaultValue: "timedraffle"),
        new("JoinCommandName", "Join command", "Viewer command to enter, without !.", DefaultValue: "jointimedraffle"),
        new("StartCommandName", "Start command", "Moderator command to start the timed raffle, without !.", DefaultValue: "starttimedraffle"),
        new("EndCommandName", "End command", "Moderator command to end the raffle early, without !.", DefaultValue: "endtimedraffle"),
        new("PointTypeId", "Award point type", "Choose a point type, or leave on Default to use the raffle key's default point mapping.", Required: false),
        new("IncludeStarterMessages", "Include raffle chat messages", "Announce raffle start, joins, and end results.", DefaultValue: "true"),
        new("DurationSeconds", "Duration (seconds)", "Raffle duration from 60 to 86400 seconds.", DefaultValue: "600"),
        new("AnnouncementIntervalSeconds", "Announcement cadence (seconds)", "Repeat an announcement every 30 to 3600 seconds.", DefaultValue: "120"),
        new("WinnerCount", "Winner count", "Number of raffle winners.", DefaultValue: "1"),
        new("UseRandomTotalAward", "Randomize total award", "Roll the total points pool once when the raffle starts.", DefaultValue: "false"),
        new("RandomTotalAwardMinimum", "Random award minimum", "Inclusive minimum total award.", DefaultValue: "1", Required: false),
        new("RandomTotalAwardMaximum", "Random award maximum", "Inclusive maximum total award.", DefaultValue: "100", Required: false),
        new("TotalAward", "Total award", "Fixed total points pool split among winners.", DefaultValue: "0", Required: false),
        new("Announcement1", "Announcement 1", "First periodic announcement.", ActionRecipeParameterType.Multiline, "%raffle_name% is still open! Join with !%raffle_join_command%.", ReplaceDefaultWhenInsertingVariable: true),
        new("Announcement2", "Announcement 2 (optional)", "Optional second announcement, alternated with the first.", ActionRecipeParameterType.Multiline, Required: false),
        new("Announcement3", "Announcement 3 (optional)", "Optional third announcement, alternated with the others.", ActionRecipeParameterType.Multiline, Required: false)
    ];

    public string Id => "timed-raffle";
    public string Title => "Timed raffle";
    public string Category => "Community";
    public string Icon => "mdi-ticket-confirmation";
    public string Description => "Create join and start commands for a timed raffle with periodic announcements and automatic closing.";
    public IReadOnlyList<ActionRecipeParameter> Parameters => RecipeParameters;
    public bool RequiresCommand => false;
    public bool SupportsVariables => true;
    public string? RecommendedQueueName => "Raffles";
    public string? SuggestedGroupName => "Raffles";
    public IReadOnlyList<string> VariableParameterKeys => ["Announcement1", "Announcement2", "Announcement3"];

    public PenguinTwitchBot.Bot.Actions.RaffleSetupRequest BuildRequest(
        string groupName,
        string queueName,
        IReadOnlyDictionary<string, string> parameters)
    {
        var duration = ParseInt(parameters, "DurationSeconds", 60, 86400);
        var interval = ParseInt(parameters, "AnnouncementIntervalSeconds", 30, 3600);
        var winnerCount = ParseInt(parameters, "WinnerCount", 1, 100);
        var useRandomTotalAward = bool.TryParse(GetParameter(parameters, "UseRandomTotalAward"), out var randomAward) && randomAward;
        var totalAward = useRandomTotalAward ? 0 : ParseLong(parameters, "TotalAward", 0, long.MaxValue);
        var randomTotalAwardMinimum = 0;
        var randomTotalAwardMaximum = 0;
        if (useRandomTotalAward &&
            (!int.TryParse(GetParameter(parameters, "RandomTotalAwardMinimum"), out randomTotalAwardMinimum) ||
             !int.TryParse(GetParameter(parameters, "RandomTotalAwardMaximum"), out randomTotalAwardMaximum) ||
             randomTotalAwardMinimum < 0 || randomTotalAwardMinimum > 1_000_000 ||
             randomTotalAwardMaximum > 1_000_000 || randomTotalAwardMaximum <= randomTotalAwardMinimum))
        {
            throw new ArgumentException("Random total award must be between 0 and 1,000,000, with the maximum greater than the minimum.", nameof(parameters));
        }
        var pointTypeIdText = GetParameter(parameters, "PointTypeId").Trim();
        Guid? pointTypeId = null;
        if (!string.IsNullOrEmpty(pointTypeIdText))
        {
            if (!Guid.TryParse(pointTypeIdText, out var parsedPointTypeId) || parsedPointTypeId == Guid.Empty)
                throw new ArgumentException("Choose a valid point type or Default.", nameof(parameters));
            pointTypeId = parsedPointTypeId;
        }
        if (interval > duration)
            throw new ArgumentException("Announcement cadence cannot exceed the raffle duration.", nameof(parameters));

        var announcements = Enumerable.Range(1, 3)
            .Select(index => GetParameter(parameters, $"Announcement{index}").Trim())
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToList();
        if (announcements.Count == 0)
            throw new ArgumentException("At least one periodic announcement is required.", nameof(parameters));

        return new PenguinTwitchBot.Bot.Actions.RaffleSetupRequest
        {
            RaffleName = GetParameter(parameters, "RaffleName").Trim(),
            RaffleKey = GetParameter(parameters, "RaffleKey").Trim(),
            Group = groupName,
            QueueName = queueName,
            StartQueueName = queueName,
            JoinQueueName = "Default",
            EndQueueName = "Default",
            JoinCommandName = GetParameter(parameters, "JoinCommandName").Trim(),
            StartCommandName = GetParameter(parameters, "StartCommandName").Trim(),
            EndCommandName = GetParameter(parameters, "EndCommandName").Trim(),
            CreateEndCommand = true,
            PointTypeId = pointTypeId,
            WinnerCount = winnerCount,
            TotalAward = totalAward,
            UseRandomTotalAward = useRandomTotalAward,
            RandomTotalAwardMinimum = randomTotalAwardMinimum,
            RandomTotalAwardMaximum = randomTotalAwardMaximum,
            DurationSeconds = duration,
            AnnouncementIntervalSeconds = interval,
            AnnouncementMessages = announcements,
            IncludeStarterMessages = !bool.TryParse(GetParameter(parameters, "IncludeStarterMessages"), out var includeMessages) || includeMessages
        };
    }

    public ActionType Build(ActionRecipeContext context, IReadOnlyDictionary<string, string> parameters) =>
        throw new NotSupportedException("Timed raffle recipes use the multi-action raffle setup workflow.");

    private static int ParseInt(IReadOnlyDictionary<string, string> parameters, string key, int minimum, int maximum)
    {
        if (!int.TryParse(GetParameter(parameters, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < minimum || value > maximum)
            throw new ArgumentException($"{key} must be between {minimum} and {maximum}.", nameof(parameters));
        return value;
    }

    private static long ParseLong(IReadOnlyDictionary<string, string> parameters, string key, long minimum, long maximum)
    {
        if (!long.TryParse(GetParameter(parameters, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < minimum || value > maximum)
            throw new ArgumentException($"{key} must be between {minimum} and {maximum}.", nameof(parameters));
        return value;
    }

    private static string GetParameter(IReadOnlyDictionary<string, string> parameters, string key) =>
        parameters.GetValueOrDefault(key, string.Empty);
}