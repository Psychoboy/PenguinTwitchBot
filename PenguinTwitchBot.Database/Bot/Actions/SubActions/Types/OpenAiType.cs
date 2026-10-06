using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Database.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "OpenAI Prompt",
        description: "Generate a custom AI response using OpenAI",
        icon: "mdi-robot",
        color: "Primary",
        tableName: "subactions_openai",
        category: SubActionCategories.Ai)]
    public class OpenAiType : SubActionType, ISubActionUIProvider
    {
        public OpenAiType()
        {
            SubActionTypes = SubActionTypes.OpenAi;
        }

        public string Instructions { get; set; } = string.Empty;
        public string Model { get; set; } = "gpt-5.1";
        public int MaxOutputTokenCount { get; set; } = 200;
        public string ServiceTier { get; set; } = "default";
        public bool EnableWebSearch { get; set; } = false;
        public string AllowedDomains { get; set; } = string.Empty;
        public bool SavePreviousResponse { get; set; } = false;
        public string SessionKey { get; set; } = "%UserId%";
        public string ResponseVariableName { get; set; } = "AiResponse";
        public bool CleanOutput { get; set; } = true;

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return new List<SubActionUIField>
            {
                new()
                {
                    PropertyName = nameof(Text),
                    Label = "Prompt",
                    FieldType = UIFieldType.TextArea,
                    Required = true,
                    Lines = 4,
                    Resizable = true,
                    HelperText = "Prompt to send to OpenAI. Supports variables like %user%, %rawinput%, %message%."
                },
                new()
                {
                    PropertyName = nameof(Instructions),
                    Label = "Instructions (System Prompt)",
                    FieldType = UIFieldType.TextArea,
                    Lines = 5,
                    Resizable = true,
                    HelperText = "System behavior instructions for the AI. Supports variables."
                },
                new()
                {
                    PropertyName = nameof(Model),
                    Label = "Model",
                    FieldType = UIFieldType.Select,
                    Required = true,
                    AllowCustomValue = true,
                    Options = new[] { "gpt-5.1", "gpt-4.1", "gpt-4.1-mini", "gpt-4.1-nano", "gpt-4o", "gpt-4o-mini", "o3-mini", "o1" },
                    HelperText = "Select or type an OpenAI model name."
                },
                new()
                {
                    PropertyName = nameof(MaxOutputTokenCount),
                    Label = "Max Output Tokens",
                    FieldType = UIFieldType.Number,
                    Min = 1,
                    Max = 4096,
                    DefaultValue = 200,
                    HelperText = "Maximum token count for the response (default: 200)."
                },
                new()
                {
                    PropertyName = nameof(ServiceTier),
                    Label = "Service Tier",
                    FieldType = UIFieldType.Select,
                    AllowCustomValue = true,
                    Options = new[] { "default", "flex", "auto" },
                    HelperText = "Processing tier ('flex' offers lower cost for eligible models)."
                },
                new()
                {
                    PropertyName = nameof(EnableWebSearch),
                    Label = "Enable Web Search Tool",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Primary",
                    HelperText = "Allow OpenAI to browse the web for up-to-date information."
                },
                new()
                {
                    PropertyName = nameof(AllowedDomains),
                    Label = "Allowed Search Domains (Optional)",
                    FieldType = UIFieldType.TextArea,
                    Lines = 2,
                    HelperText = "Comma or newline separated domains to restrict web search (e.g. starcitizen.tools, finder.cstone.space). Leave blank for all."
                },
                new()
                {
                    PropertyName = nameof(SavePreviousResponse),
                    Label = "Remember Conversation History",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Secondary",
                    HelperText = "Maintains conversational memory using OpenAI PreviousResponseId."
                },
                new()
                {
                    PropertyName = nameof(SessionKey),
                    Label = "Conversation Session Key",
                    FieldType = UIFieldType.Text,
                    DefaultValue = "%UserId%",
                    HelperText = "Identifier parameter used to track conversation history per user (e.g., %UserId% or %user%)."
                },
                new()
                {
                    PropertyName = nameof(ResponseVariableName),
                    Label = "Output Variable Name",
                    FieldType = UIFieldType.Text,
                    Required = true,
                    DefaultValue = "AiResponse",
                    HelperText = "Name of the variable to store the AI response in (e.g. %AiResponse%)."
                },
                new()
                {
                    PropertyName = nameof(CleanOutput),
                    Label = "Sanitize For Twitch Chat",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Success",
                    HelperText = "Removes markdown link formatting and converts line breaks into single-line text."
                },
                new()
                {
                    PropertyName = nameof(Enabled),
                    Label = "Enabled",
                    FieldType = UIFieldType.Switch,
                    SwitchColor = "Success"
                }
            };
        }

        public Dictionary<string, object?> GetValues()
        {
            return new Dictionary<string, object?>
            {
                { nameof(Text), Text },
                { nameof(Instructions), Instructions },
                { nameof(Model), Model },
                { nameof(MaxOutputTokenCount), MaxOutputTokenCount },
                { nameof(ServiceTier), ServiceTier },
                { nameof(EnableWebSearch), EnableWebSearch },
                { nameof(AllowedDomains), AllowedDomains },
                { nameof(SavePreviousResponse), SavePreviousResponse },
                { nameof(SessionKey), SessionKey },
                { nameof(ResponseVariableName), ResponseVariableName },
                { nameof(CleanOutput), CleanOutput },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Text), out var text))
                Text = text as string ?? string.Empty;
            if (values.TryGetValue(nameof(Instructions), out var instructions))
                Instructions = instructions as string ?? string.Empty;
            if (values.TryGetValue(nameof(Model), out var model))
                Model = !string.IsNullOrWhiteSpace(model as string) ? (string)model! : "gpt-5.1";
            if (values.TryGetValue(nameof(MaxOutputTokenCount), out var maxTokens))
                MaxOutputTokenCount = maxTokens is int tokens && tokens > 0 ? tokens : 200;
            if (values.TryGetValue(nameof(ServiceTier), out var tier))
                ServiceTier = !string.IsNullOrWhiteSpace(tier as string) ? (string)tier! : "default";
            if (values.TryGetValue(nameof(EnableWebSearch), out var search))
                EnableWebSearch = search as bool? ?? false;
            if (values.TryGetValue(nameof(AllowedDomains), out var domains))
                AllowedDomains = domains as string ?? string.Empty;
            if (values.TryGetValue(nameof(SavePreviousResponse), out var savePrev))
                SavePreviousResponse = savePrev as bool? ?? false;
            if (values.TryGetValue(nameof(SessionKey), out var sessionKey))
                SessionKey = sessionKey as string ?? "%UserId%";
            if (values.TryGetValue(nameof(ResponseVariableName), out var respVar))
                ResponseVariableName = !string.IsNullOrWhiteSpace(respVar as string) ? (string)respVar! : "AiResponse";
            if (values.TryGetValue(nameof(CleanOutput), out var clean))
                CleanOutput = clean as bool? ?? true;
            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if (!values.TryGetValue(nameof(Text), out var text) || string.IsNullOrWhiteSpace(text as string))
                return "Prompt is required";

            if (!values.TryGetValue(nameof(Model), out var model) || string.IsNullOrWhiteSpace(model as string))
                return "Model is required";

            return null;
        }
    }
}

