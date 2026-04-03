using Microsoft.AspNetCore.Components;

namespace DotNetTwitchBot.Bot.Actions.SubActions.UI
{
    /// <summary>
    /// Interface for SubActions to provide their own UI configuration.
    /// Implement this on SubAction types that need custom UI fields.
    /// </summary>
    public interface ISubActionUIProvider
    {
        /// <summary>
        /// Returns the list of UI fields to display in the configuration dialog.
        /// </summary>
        List<SubActionUIField> GetUIFields();

        /// <summary>
        /// Load values from the current instance into a dictionary.
        /// </summary>
        Dictionary<string, object?> GetValues();

        /// <summary>
        /// Apply values from a dictionary to the current instance.
        /// </summary>
        void SetValues(Dictionary<string, object?> values);

        /// <summary>
        /// Validate the field values. Return null if valid, or error message if invalid.
        /// </summary>
        /// <param name="values">The field values to validate</param>
        string? Validate(Dictionary<string, object?> values);
    }

    /// <summary>
    /// Describes a UI field for SubAction configuration.
    /// </summary>
    public class SubActionUIField
    {
        public string PropertyName { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? HelperText { get; set; }
        public UIFieldType FieldType { get; set; }
        public bool Required { get; set; }
        public object? DefaultValue { get; set; }
        public Dictionary<string, object> Attributes { get; set; } = new();
    }

    public enum UIFieldType
    {
        Text,
        TextArea,
        Number,
        Switch,
        Select,
        Float
    }
}
