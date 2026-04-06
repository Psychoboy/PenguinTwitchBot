namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    /// <summary>
    /// Example: Runtime-only SubAction for executing commands programmatically.
    /// This will NOT appear in the UI or be persisted to the database.
    /// </summary>
    public class RuntimeDefaultCommandType : RuntimeOnlySubActionType
    {
        public RuntimeDefaultCommandType()
        {
            // You can use an existing SubActionTypes enum value or create a new one
            // For purely runtime actions, you might reuse an existing type or add a new enum
            SubActionTypes = SubActionTypes.RuntimeDefaultCommand;
        }
    }
}
