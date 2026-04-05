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
        /// <param name="serviceProvider">Optional service provider for accessing services to populate dynamic fields.
        /// Example usage:
        /// <code>
        /// public List&lt;SubActionUIField&gt; GetUIFields(IServiceProvider? serviceProvider = null)
        /// {
        ///     List&lt;string&gt;? rewardNames = null;
        ///     
        ///     if (serviceProvider != null)
        ///     {
        ///         using var scope = serviceProvider.CreateScope();
        ///         var rewardService = scope.ServiceProvider.GetRequiredService&lt;IRewardService&gt;();
        ///         rewardNames = rewardService.GetRewardNames(); // Must be synchronous
        ///     }
        ///     
        ///     return new List&lt;SubActionUIField&gt;
        ///     {
        ///         new()
        ///         {
        ///             PropertyName = "RewardName",
        ///             Label = "Reward",
        ///             FieldType = UIFieldType.Select,
        ///             Options = rewardNames?.ToArray()
        ///         }
        ///     };
        /// }
        /// </code>
        /// </param>
        List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null);

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
        // Common properties for all field types
        public string PropertyName { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? HelperText { get; set; }
        public UIFieldType FieldType { get; set; }
        public bool Required { get; set; }
        public object? DefaultValue { get; set; }

        // Text/TextArea specific
        public int? Lines { get; set; }

        // Number/Float specific
        public object? Min { get; set; }
        public object? Max { get; set; }
        public object? Step { get; set; }
        public bool Clearable { get; set; } = false;

        // Switch specific
        public string? SwitchColor { get; set; } = "Primary";

        // Select specific
        public string[]? Options { get; set; }
        public List<SelectOption>? SelectOptions { get; set; }

        // Info/Alert specific
        public string? Severity { get; set; } = "Info";  // Info, Success, Warning, Error, Normal
        public string? InfoVariant { get; set; }  // Text, Filled, Outlined
        public string? Icon { get; set; }
        public bool Dense { get; set; }
        public bool NoIcon { get; set; }

        // Fallback for any custom attributes not covered above
        [Obsolete("Use strongly-typed properties instead")]
        public Dictionary<string, object> Attributes { get; set; } = new();
    }

    public enum UIFieldType
    {
        Text,
        TextArea,
        Number,
        Switch,
        Select,
        Float,
        Info  // Display-only informational message (like MudAlert)
    }

    /// <summary>
    /// Represents a dropdown option with separate display name and value.
    /// </summary>
    public class SelectOption
    {
        public string Name { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
    }
}
