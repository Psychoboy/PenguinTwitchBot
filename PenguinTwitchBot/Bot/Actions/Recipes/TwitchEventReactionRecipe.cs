using System.Globalization;
using PenguinTwitchBot.Bot.Actions.Triggers;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.TwitchApi.Models.ChannelPoints;

namespace PenguinTwitchBot.Bot.Actions.Recipes;

public abstract class TwitchResponseRecipeBase : IActionRecipe
{
    private readonly HashSet<string> _supportedEvents;
    private readonly string _id;
    private readonly string _title;
    private readonly string _category;
    private readonly string _icon;
    private readonly string _description;
    private readonly string _group;
    private readonly string _triggerRequirementDescription;

    private static readonly IReadOnlyList<ActionRecipeParameter> RecipeParameters =
    [
        new("MessageText", "Chat message", "Message to send when the selected Twitch event occurs.", ActionRecipeParameterType.Multiline, "Thanks, %user%!", ReplaceDefaultWhenInsertingVariable: true),
        new("AlertEnabled", "Add an overlay alert", "Show an alert alongside the chat response.", DefaultValue: "false"),
        new("AlertText", "Alert text", "Text shown in the overlay alert.", ActionRecipeParameterType.Multiline, Required: false),
        new("AlertFile", "Alert media", "Optional image or video from the Media Library.", Required: false),
        new("AlertAudioFile", "Alert audio", "Optional audio file from the Media Library.", Required: false),
        new("AlertDuration", "Alert duration (seconds)", "Duration from 1 to 60 seconds.", DefaultValue: "6"),
        new("AlertVolume", "Alert volume", "Volume from 0 to 1.", DefaultValue: "0.8"),
        new("SoundFile", "Additional sound", "Optional sound to play from the Media Library.", Required: false),
        new("FireworksCount", "Fireworks bursts", "Use 0 for none, or 1 to 100. Requires a Fireworks overlay widget.", DefaultValue: "0")
    ];
    protected static IReadOnlyList<ActionRecipeParameter> ResponseParameters => RecipeParameters;

    protected TwitchResponseRecipeBase(
        string id,
        string title,
        string category,
        string icon,
        string description,
        string group,
        string triggerRequirementDescription,
        IEnumerable<string> supportedEvents)
    {
        _id = id;
        _title = title;
        _category = category;
        _icon = icon;
        _description = description;
        _group = group;
        _triggerRequirementDescription = triggerRequirementDescription;
        _supportedEvents = new HashSet<string>(supportedEvents, StringComparer.OrdinalIgnoreCase);
    }

    public string Id => _id;
    public string Title => _title;
    public string Category => _category;
    public string Icon => _icon;
    public string Description => _description;
    public virtual IReadOnlyList<ActionRecipeParameter> Parameters => RecipeParameters;
    public bool RequiresCommand => false;
    public bool SupportsVariables => true;
    public virtual bool AllowsAdditionalTriggers => true;
    public virtual bool RequiresTrigger => true;
    public string TriggerRequirementDescription => _triggerRequirementDescription;
    public string TriggerButtonLabel => "Add Twitch event trigger";
    public IReadOnlyCollection<TriggerTypes>? AllowedTriggerTypes => [TriggerTypes.TwitchEvent];
    public IReadOnlyCollection<string>? AllowedTwitchEventNames => _supportedEvents;
    public string? SuggestedGroupName => _group;
    public IReadOnlyList<string> VariableParameterKeys => ["MessageText", "AlertText"];

    public bool IsRequiredTrigger(TriggerType trigger)
    {
        if (trigger.Type != TriggerTypes.TwitchEvent)
            return false;

        return _supportedEvents.Contains(TwitchEventTriggerConfig.FromJson(trigger.Configuration).EventName);
    }

    public virtual ActionType Build(ActionRecipeContext context, IReadOnlyDictionary<string, string> parameters)
    {
        var actionName = context.ActionName.Trim();
        if (string.IsNullOrWhiteSpace(actionName))
            throw new ArgumentException("Action name is required.", nameof(context));
        if (string.IsNullOrWhiteSpace(context.QueueName))
            throw new ArgumentException("Queue name is required.", nameof(context));

        var message = GetParameter(parameters, "MessageText").Trim();
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("A chat message is required.", nameof(parameters));
        if (!int.TryParse(GetParameter(parameters, "FireworksCount"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var fireworksCount) || fireworksCount is < 0 or > 100)
            throw new ArgumentException("Fireworks bursts must be between 0 and 100.", nameof(parameters));

        var subActions = new List<SubActionType>
        {
            new SendMessageType { Index = 0, Text = message, Enabled = true }
        };
        var index = 1;

        if (bool.TryParse(GetParameter(parameters, "AlertEnabled"), out var alertEnabled) && alertEnabled)
        {
            var alertText = GetParameter(parameters, "AlertText").Trim();
            if (string.IsNullOrWhiteSpace(alertText))
                throw new ArgumentException("Alert text is required when the overlay alert is enabled.", nameof(parameters));
            if (!int.TryParse(GetParameter(parameters, "AlertDuration"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var duration) || duration is < 1 or > 60)
                throw new ArgumentException("Alert duration must be between 1 and 60 seconds.", nameof(parameters));
            if (!float.TryParse(GetParameter(parameters, "AlertVolume"), NumberStyles.Float, CultureInfo.InvariantCulture, out var volume) || volume is < 0 or > 1)
                throw new ArgumentException("Alert volume must be between 0 and 1.", nameof(parameters));

            subActions.Add(new AlertType
            {
                Index = index++,
                Text = alertText,
                File = GetParameter(parameters, "AlertFile").Trim(),
                AudioFile = GetParameter(parameters, "AlertAudioFile").Trim(),
                Duration = duration,
                Volume = volume,
                CSS = GetParameter(parameters, "AlertCss"),
                Enabled = true
            });
        }

        var soundFile = GetParameter(parameters, "SoundFile").Trim();
        if (!string.IsNullOrWhiteSpace(soundFile))
            subActions.Add(new PlaySoundType { Index = index++, File = soundFile, Enabled = true });

        if (fireworksCount > 0)
            subActions.Add(new FireworksType { Index = index, Count = fireworksCount.ToString(CultureInfo.InvariantCulture), Enabled = true });

        return new ActionType
        {
            Name = actionName,
            Group = _group,
            QueueName = context.QueueName,
            SubActions = subActions,
            Triggers = BuildTriggers(parameters)
        };
    }

    protected virtual List<TriggerType> BuildTriggers(IReadOnlyDictionary<string, string> parameters) => [];

    protected static string GetParameter(IReadOnlyDictionary<string, string> parameters, string key) =>
        parameters.GetValueOrDefault(key, string.Empty);
}

public sealed class TwitchEventReactionRecipe : TwitchResponseRecipeBase
{
    public TwitchEventReactionRecipe()
        : base(
            "twitch-event-reaction",
            "Twitch event reaction",
            "Twitch",
            "mdi-twitch",
            "Respond to a follow, subscription, gift, cheer, or raid with chat, alert, sound, or fireworks.",
            "Recipes / Twitch Events",
            "follow, subscription, gift, cheer, or raid event",
            ["ChannelFollow", "ChannelSubscribe", "ChannelSubscriptionMessage", "ChannelSubscriptionGift", "ChannelCheer", "ChannelBitsUse", "ChannelRaid"])
    {
    }
}

public sealed class ChannelPointActionRecipe : TwitchResponseRecipeBase
{
    private static readonly IReadOnlyList<ActionRecipeParameter> ChannelPointParameters =
    [
        .. ResponseParameters,
        new("RewardMode", "Reward setup", "Use an existing reward or create a new Twitch channel point reward.", DefaultValue: "Existing"),
        new("RewardTitle", "Reward title", "Existing or newly created reward title; set by the recipe wizard.", Required: false),
        new("NewRewardTitle", "New reward title", "Title for the new Twitch reward.", Required: false),
        new("RewardCost", "Reward cost", "Cost in channel points for a new reward.", DefaultValue: "100"),
        new("RewardPrompt", "Reward prompt (optional)", "Prompt shown to viewers for a new reward.", Required: false),
        new("RewardRequiresInput", "Require viewer input", "Require text input when redeeming a new reward.", DefaultValue: "false"),
        new("RewardSkipQueue", "Skip queue", "When enabled, Twitch fulfills redemptions automatically instead of adding them to the request queue.", DefaultValue: "false")
    ];

    public ChannelPointActionRecipe()
        : base(
            "channel-point-action",
            "Channel point redeem",
            "Twitch",
            "mdi-star-circle-outline",
            "Respond to a channel point redemption with chat, alert, sound, or fireworks.",
            "Recipes / Channel Points",
            "Channel Point Redemption trigger",
            ["ChannelPointsCustomRewardRedemptionAdd"])
    {
    }

    public override IReadOnlyList<ActionRecipeParameter> Parameters => ChannelPointParameters;
    public override bool AllowsAdditionalTriggers => false;
    public override bool RequiresTrigger => false;

    protected override List<TriggerType> BuildTriggers(IReadOnlyDictionary<string, string> parameters)
    {
        var rewardTitle = GetParameter(parameters, "RewardTitle").Trim();
        if (string.IsNullOrWhiteSpace(rewardTitle))
            throw new ArgumentException("Choose or create a channel point reward.", nameof(parameters));

        return
        [
            new TriggerType
            {
                Name = "ChannelPointsCustomRewardRedemptionAdd",
                Type = TriggerTypes.TwitchEvent,
                Configuration = System.Text.Json.JsonSerializer.Serialize(new TwitchEventTriggerConfig
                {
                    EventName = "ChannelPointsCustomRewardRedemptionAdd",
                    RewardTitles = [rewardTitle]
                }),
                Enabled = true
            }
        ];
    }

    public CreateChannelPointRewardRequest BuildRewardRequest(IReadOnlyDictionary<string, string> parameters)
    {
        var title = GetParameter(parameters, "NewRewardTitle").Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Reward title is required.", nameof(parameters));
        if (!int.TryParse(GetParameter(parameters, "RewardCost"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var cost) || cost < 1)
            throw new ArgumentException("Reward cost must be at least 1 point.", nameof(parameters));

        return new CreateChannelPointRewardRequest
        {
            Title = title,
            Cost = cost,
            Prompt = GetParameter(parameters, "RewardPrompt").Trim(),
            IsEnabled = true,
            IsUserInputRequired = bool.TryParse(GetParameter(parameters, "RewardRequiresInput"), out var requiresInput) && requiresInput,
            ShouldRedemptionsSkipRequestQueue = bool.TryParse(GetParameter(parameters, "RewardSkipQueue"), out var skipQueue) && skipQueue,
            BackgroundColor = "#9147FF"
        };
    }
}