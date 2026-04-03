namespace DotNetTwitchBot.Bot.Actions.SubActions
{
    /// <summary>
    /// Attribute to provide metadata for SubAction types.
    /// This eliminates the need to manually register SubActions in multiple places.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class SubActionMetadataAttribute : Attribute
    {
        public string DisplayName { get; }
        public string Description { get; }
        public string Icon { get; }
        public string Color { get; }
        public string TableName { get; }

        public SubActionMetadataAttribute(
            string displayName, 
            string description, 
            string icon = "mdi-cog", 
            string color = "Primary",
            string? tableName = null)
        {
            DisplayName = displayName;
            Description = description;
            Icon = icon;
            Color = color;
            TableName = tableName ?? $"SubActions_{displayName.Replace(" ", "")}";
        }
    }
}
