using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Select Random Viewers",
        description: "Select unique random viewers from the current viewer list and add them as action variables.",
        icon: "mdi-account-multiple-plus",
        color: "Secondary",
        tableName: "subactions_selectrandomviewers")]
    public class SelectRandomViewersType : SubActionType, ISubActionUIProvider
    {
        public int ViewerCount { get; set; } = 1;
        public string ExcludedViewers { get; set; } = string.Empty;

        public SelectRandomViewersType()
        {
            SubActionTypes = SubActionTypes.SelectRandomViewers;
        }

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return
            [
                new()
                {
                    PropertyName = nameof(ViewerCount),
                    Label = "Number of Viewers",
                    FieldType = UIFieldType.Number,
                    Required = true,
                    Min = 1,
                    Max = 100,
                    HelperText = "If fewer eligible viewers are available, all available viewers are selected."
                },
                new()
                {
                    PropertyName = nameof(ExcludedViewers),
                    Label = "Excluded Viewers",
                    FieldType = UIFieldType.TextArea,
                    Lines = 4,
                    HelperText = "Optional. Enter usernames or variables such as %user%, separated by commas or new lines."
                },
                new()
                {
                    PropertyName = "info_variables",
                    Label = "Adds %selected_viewer_1%, %selected_viewer_2%, etc. and %selected_viewer_count%.",
                    FieldType = UIFieldType.Info,
                    Severity = "Info",
                    Dense = true
                },
                new()
                {
                    PropertyName = nameof(Enabled),
                    Label = "Enabled",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Success"
                }
            ];
        }

        public Dictionary<string, object?> GetValues() => new()
        {
            { nameof(ViewerCount), ViewerCount },
            { nameof(ExcludedViewers), ExcludedViewers },
            { nameof(Enabled), Enabled }
        };

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(ViewerCount), out var viewerCount) &&
                int.TryParse(viewerCount?.ToString(), out var parsedViewerCount))
            {
                ViewerCount = parsedViewerCount;
            }

            if (values.TryGetValue(nameof(ExcludedViewers), out var excludedViewers))
                ExcludedViewers = excludedViewers?.ToString() ?? string.Empty;

            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(ViewerCount), out var viewerCount) ||
                !int.TryParse(viewerCount?.ToString(), out var parsedViewerCount) ||
                parsedViewerCount is < 1 or > 100)
            {
                return "Number of Viewers must be between 1 and 100";
            }

            return null;
        }
    }
}