namespace PenguinTwitchBot.Bot.Models;

public sealed class HomepageLayoutConfig
{
    public List<HomepageWidgetConfig> Widgets { get; set; } = [];
}

public sealed class HomepageWidgetConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Type { get; set; } = string.Empty;

    public string? Title { get; set; }

    public int? SongLimit { get; set; }

    public int? Months { get; set; }
}

public sealed class HomepageWidgetDefinition
{
    public HomepageWidgetDefinition(
        string type,
        string displayName,
        string description,
        bool supportsTitle = false,
        bool supportsSongLimit = false,
        bool supportsMonths = false,
        bool streamerOnly = false)
    {
        Type = type;
        DisplayName = displayName;
        Description = description;
        SupportsTitle = supportsTitle;
        SupportsSongLimit = supportsSongLimit;
        SupportsMonths = supportsMonths;
        StreamerOnly = streamerOnly;
    }

    public string Type { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public bool SupportsTitle { get; }

    public bool SupportsSongLimit { get; }

    public bool SupportsMonths { get; }

    public bool StreamerOnly { get; }
}

public static class HomepageWidgetCatalog
{
    public const string Giveaway = "giveaway";
    public const string StreamerTools = "streamer-tools";
    public const string StreamSchedule = "stream-schedule";
    public const string SongRequests = "song-requests";
    public const string TopRequestedSongs = "top-requested-songs";

    public static readonly IReadOnlyList<HomepageWidgetDefinition> Definitions =
    [
        new HomepageWidgetDefinition(
            Giveaway,
            "Giveaway",
            "Shows bonus points and the current giveaway."),
        new HomepageWidgetDefinition(
            StreamerTools,
            "Streamer Tools",
            "Quick controls and the message sender, visible to the streamer only.",
            streamerOnly: true),
        new HomepageWidgetDefinition(
            StreamSchedule,
            "Stream Schedule",
            "Shows the next scheduled streams."),
        new HomepageWidgetDefinition(
            SongRequests,
            "Song Requests",
            "Shows the current song request queue.",
            supportsTitle: true,
            supportsSongLimit: true),
        new HomepageWidgetDefinition(
            TopRequestedSongs,
            "Top Requested Songs",
            "Shows the most requested songs.",
            supportsTitle: true,
            supportsMonths: true),
    ];

    public static HomepageLayoutConfig CreateDefaultLayout() => new()
    {
        Widgets =
        [
            CreateDefaultWidget(Giveaway),
            CreateDefaultWidget(StreamerTools),
            CreateDefaultWidget(StreamSchedule),
            CreateDefaultWidget(SongRequests),
            CreateDefaultWidget(TopRequestedSongs),
        ]
    };

    public static HomepageWidgetConfig CreateDefaultWidget(string type)
    {
        return type switch
        {
            Giveaway => new HomepageWidgetConfig { Type = Giveaway },
            StreamerTools => new HomepageWidgetConfig { Type = StreamerTools },
            StreamSchedule => new HomepageWidgetConfig { Type = StreamSchedule },
            SongRequests => new HomepageWidgetConfig
            {
                Type = SongRequests,
                Title = "Next 5 Song Requests",
                SongLimit = 5
            },
            TopRequestedSongs => new HomepageWidgetConfig
            {
                Type = TopRequestedSongs,
                Title = "Top 5 Requested Songs",
                Months = 3
            },
            _ => new HomepageWidgetConfig { Type = type }
        };
    }

    public static HomepageLayoutConfig Normalize(HomepageLayoutConfig? layout)
    {
        var normalized = new HomepageLayoutConfig();

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

    public static HomepageWidgetConfig Normalize(HomepageWidgetConfig? widget)
    {
        var normalized = widget ?? new HomepageWidgetConfig();

        if (normalized.Id == Guid.Empty)
        {
            normalized.Id = Guid.NewGuid();
        }

        normalized.Type = string.IsNullOrWhiteSpace(normalized.Type)
            ? Giveaway
            : normalized.Type.Trim();
        normalized.Title = string.IsNullOrWhiteSpace(normalized.Title) ? null : normalized.Title.Trim();

        return normalized;
    }

    public static bool TryGetDefinition(string type, out HomepageWidgetDefinition definition)
    {
        definition = Definitions.FirstOrDefault(x => string.Equals(x.Type, type, StringComparison.OrdinalIgnoreCase))
            ?? new HomepageWidgetDefinition(type, type, "This widget type is not available in the current build.");
        return Definitions.Any(x => string.Equals(x.Type, type, StringComparison.OrdinalIgnoreCase));
    }

    public static string GetDisplayName(string type)
    {
        return TryGetDefinition(type, out var definition) ? definition.DisplayName : type;
    }

    public static string GetDescription(string type)
    {
        return TryGetDefinition(type, out var definition) ? definition.Description : "Unknown widget type.";
    }
}