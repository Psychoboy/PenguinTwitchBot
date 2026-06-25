using PenguinTwitchBot.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Gift Points",
        description: "Gift points to another user",
        icon: "mdi-gift",
        color: "Default",
        tableName: "subactions_giftpoints")]
    public class GiftPointsType : SubActionType, ISubActionUIProvider
    {
        public GiftPointsType() { SubActionTypes = SubActionTypes.GiftPoints; }
        public string FromUsername { get; set; } = "%user%";
        public string TargetName { get; set; } = "%target%";
        public string Amount { get; set; } = "%Args_1%";
        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            return [];
        }

        public Dictionary<string, object?> GetValues()
        {
            return new Dictionary<string, object?>
            {
                { nameof(Text), Text },
                { nameof(FromUsername), FromUsername },
                { nameof(TargetName), TargetName },
                { nameof(Amount), Amount },
                { nameof(Enabled), Enabled }
            };
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            if(values.TryGetValue(nameof(Text), out var textValue) && textValue is string text)
                Text = text;
            if(values.TryGetValue(nameof(FromUsername), out var fromUsernameValue) && fromUsernameValue is string fromUsername)
                FromUsername = fromUsername;
            if (values.TryGetValue(nameof(TargetName), out var targetNameValue) && targetNameValue is string targetName)
                TargetName = targetName;
            if(values.TryGetValue(nameof(Amount), out var amountValue) && amountValue is string amount)
                Amount = amount;
            if(values.TryGetValue(nameof(Enabled), out var enabledValue) && enabledValue is bool enabled)
                Enabled = enabled;
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            if(!values.TryGetValue(nameof(Text), out var textValue) || textValue is not string text || string.IsNullOrWhiteSpace(text))
            {
                return "Point Name is required.";
            }
            if(!values.TryGetValue(nameof(FromUsername), out var fromUsernameValue) || fromUsernameValue is not string fromUsername || string.IsNullOrWhiteSpace(fromUsername))
            {
                return "From Username is required.";
            }
            if (!values.TryGetValue(nameof(TargetName), out var targetNameValue) || targetNameValue is not string targetName || string.IsNullOrWhiteSpace(targetName))
            {
                return "Target Username is required.";
            }
            if(!values.TryGetValue(nameof(Amount), out var amountValue) || amountValue is not string amount || string.IsNullOrWhiteSpace(amount))
            {
                return "Amount is required.";
            }
            return null;
        }
    }
}
