namespace PenguinTwitchBot.Bot.Models;

public sealed class LeaderboardsLayoutConfig
{
    public List<LeaderboardWidgetConfig> Widgets { get; set; } = [];
}

public sealed class LeaderboardWidgetConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Type { get; set; } = string.Empty;

    public string? Title { get; set; }

    public int? PointTypeId { get; set; }
}

public sealed class LeaderboardWidgetDefinition
{
    public LeaderboardWidgetDefinition(string type, string displayName, string description, bool supportsTitle = false, bool supportsPointType = false)
    {
        Type = type;
        DisplayName = displayName;
        Description = description;
        SupportsTitle = supportsTitle;
        SupportsPointType = supportsPointType;
    }

    public string Type { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public bool SupportsTitle { get; }

    public bool SupportsPointType { get; }
}

public static class LeaderboardsWidgetCatalog
{
    public const string PointLeaderboard = "point-leaderboard";
    public const string Loudest = "loudest";
    public const string WatchedTime = "watched-time";

    public static readonly IReadOnlyList<LeaderboardWidgetDefinition> Definitions =
    [
        new LeaderboardWidgetDefinition(
            PointLeaderboard,
            "Point Leaderboard",
            "Shows a leaderboard for any point type.",
            supportsTitle: true,
            supportsPointType: true),
        new LeaderboardWidgetDefinition(
            Loudest,
            "Loudest",
            "Shows the loudest viewers by message count.",
            supportsTitle: true),
        new LeaderboardWidgetDefinition(
            WatchedTime,
            "Watched Time",
            "Shows the viewers with the most watched time.",
            supportsTitle: true),
    ];

    public static LeaderboardWidgetConfig CreateDefaultWidget(string type, int? defaultPointTypeId = null, string? defaultTitle = null)
    {
        return type switch
        {
            PointLeaderboard => new LeaderboardWidgetConfig
            {
                Type = PointLeaderboard,
                PointTypeId = defaultPointTypeId,
                Title = defaultTitle
            },
            Loudest => new LeaderboardWidgetConfig
            {
                Type = Loudest,
                Title = defaultTitle ?? "Loudest"
            },
            WatchedTime => new LeaderboardWidgetConfig
            {
                Type = WatchedTime,
                Title = defaultTitle ?? "Watched Time"
            },
            _ => new LeaderboardWidgetConfig { Type = type, Title = defaultTitle }
        };
    }

    public static LeaderboardsLayoutConfig CreateDefaultLayout(int? ticketPointTypeId = null, int? pastiesPointTypeId = 1)
    {
        return new LeaderboardsLayoutConfig
        {
            Widgets =
            [
                CreateDefaultWidget(PointLeaderboard, ticketPointTypeId, "Tickets"),
                CreateDefaultWidget(PointLeaderboard, pastiesPointTypeId, "Pasties"),
                CreateDefaultWidget(Loudest),
                CreateDefaultWidget(WatchedTime),
            ]
        };
    }

    public static LeaderboardsLayoutConfig Normalize(LeaderboardsLayoutConfig? layout)
    {
        var normalized = new LeaderboardsLayoutConfig();

        if (layout?.Widgets == null)
        {
            return normalized;
        }

        foreach (var widget in layout.Widgets)
        {
            normalized.Widgets.Add(Normalize(widget));
        }

        return normalized;
    }

    public static LeaderboardWidgetConfig Normalize(LeaderboardWidgetConfig? widget)
    {
        var normalized = widget ?? new LeaderboardWidgetConfig();

        if (normalized.Id == Guid.Empty)
        {
            normalized.Id = Guid.NewGuid();
        }

        normalized.Type = string.IsNullOrWhiteSpace(normalized.Type) ? PointLeaderboard : normalized.Type.Trim();
        normalized.Title = string.IsNullOrWhiteSpace(normalized.Title) ? null : normalized.Title.Trim();
        return normalized;
    }

    public static bool TryGetDefinition(string type, out LeaderboardWidgetDefinition definition)
    {
        definition = Definitions.FirstOrDefault(x => string.Equals(x.Type, type, StringComparison.OrdinalIgnoreCase))
            ?? new LeaderboardWidgetDefinition(type, type, "This leaderboard type is not available in the current build.");
        return Definitions.Any(x => string.Equals(x.Type, type, StringComparison.OrdinalIgnoreCase));
    }
}