using DotNetTwitchBot.Bot.Actions.SubActions.UI;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    /// <summary>
    /// Base class for simple SubActions that only need Text and Enabled fields.
    /// Inherit from this to get default UI implementation.
    /// </summary>
    public abstract class SimpleSubActionType : SubActionType, ISubActionUIProvider
    {
        protected virtual string TextLabel => "Text/Value";
        protected virtual string TextHelperText => "Use variables like %user%, %target%, etc.";
        protected virtual bool TextRequired => false;
        protected virtual int TextLines => 1;

        public virtual List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            var fields = new List<SubActionUIField>
            {
                new()
                {
                    PropertyName = nameof(Text),
                    Label = TextLabel,
                    FieldType = TextLines > 1 ? UIFieldType.TextArea : UIFieldType.Text,
                    Required = TextRequired,
                    HelperText = TextHelperText,
                    Lines = TextLines > 1 ? TextLines : null
                }
            };

            AddCustomFields(fields);

            fields.Add(new()
            {
                PropertyName = nameof(Enabled),
                Label = "Enabled",
                FieldType = UIFieldType.Switch,
                SwitchColor = "Success"
            });

            return fields;
        }

        /// <summary>
        /// Override this to add additional fields beyond Text and Enabled.
        /// </summary>
        protected virtual void AddCustomFields(List<SubActionUIField> fields)
        {
        }

        public virtual Dictionary<string, object?> GetValues()
        {
            var values = new Dictionary<string, object?>
            {
                { nameof(Text), Text },
                { nameof(Enabled), Enabled }
            };

            AddCustomValues(values);
            return values;
        }

        /// <summary>
        /// Override this to add values for custom properties.
        /// </summary>
        protected virtual void AddCustomValues(Dictionary<string, object?> values)
        {
        }

        public virtual void SetValues(Dictionary<string, object?> values)
        {
            if (values.TryGetValue(nameof(Text), out var text))
                Text = text as string ?? "";
            if (values.TryGetValue(nameof(Enabled), out var enabled))
                Enabled = enabled as bool? ?? true;

            SetCustomValues(values);
        }

        /// <summary>
        /// Override this to set custom property values.
        /// </summary>
        protected virtual void SetCustomValues(Dictionary<string, object?> values)
        {
        }

        public virtual string? Validate(Dictionary<string, object?> values)
        {
            if (TextRequired)
            {
                if (!values.TryGetValue(nameof(Text), out var text) || string.IsNullOrWhiteSpace(text as string))
                    return $"{TextLabel} is required";
            }

            return ValidateCustom(values);
        }

        /// <summary>
        /// Override this to add custom validation logic.
        /// </summary>
        /// <param name="values">The field values to validate</param>
        protected virtual string? ValidateCustom(Dictionary<string, object?> values)
        {
            return null;
        }
    }
}
