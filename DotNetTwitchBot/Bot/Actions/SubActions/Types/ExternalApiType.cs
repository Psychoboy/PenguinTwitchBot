using DotNetTwitchBot.Bot.Actions.SubActions.UI;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "External API",
        description: "Call an external API endpoint",
        icon: "mdi-api",
        color: "Secondary",
        tableName: "subactions_externalapi")]
    public class ExternalApiType : SubActionType, ISubActionUIProvider
    {
        public ExternalApiType()
        {
            SubActionTypes = SubActionTypes.ExternalApi;
        }

        public string HttpMethod { get; set; } = "GET";
        public string Headers { get; set; } = "Accept: text/plain";

        public List<SubActionUIField> GetUIFields()
        {
            return new List<SubActionUIField>
            {
                new()
                {
                    PropertyName = nameof(Text),
                    Label = "URL",
                    FieldType = UIFieldType.Text,
                    Required = true,
                    HelperText = "Use variables like %user%, %target%, etc."
                },
                new()
                {
                    PropertyName = nameof(HttpMethod),
                    Label = "HTTP Method",
                    FieldType = UIFieldType.Select,
                    Attributes = new Dictionary<string, object>
                    {
                        { "Options", new[] { "GET", "POST", "PUT", "DELETE", "PATCH" } }
                    }
                },
                new()
                {
                    PropertyName = nameof(Headers),
                    Label = "Headers",
                    FieldType = UIFieldType.TextArea,
                    HelperText = "One header per line (e.g., Accept: text/plain)",
                    Attributes = new Dictionary<string, object> { { "Lines", 2 } }
                },
                new()
                {
                    PropertyName = nameof(Enabled),
                    Label = "Enabled",
                    FieldType = UIFieldType.Switch,
                    Attributes = new Dictionary<string, object> { { "Color", "Success" } }
                }
            };
        }

        public Dictionary<string, object?> GetValues()
        {
            return new Dictionary<string, object?>
            {
                { nameof(Text), Text },
                { nameof(HttpMethod), HttpMethod },
                { nameof(Headers), Headers },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Text), out var text))
                Text = text as string ?? "";
            if (values.TryGetValue(nameof(HttpMethod), out var method))
                HttpMethod = method as string ?? "GET";
            if (values.TryGetValue(nameof(Headers), out var headers))
                Headers = headers as string ?? "Accept: text/plain";
            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(Text), out var text) || string.IsNullOrWhiteSpace(text as string))
                return "URL is required";
            return null;
        }
    }
}
