namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    /// <summary>
    /// Base class for runtime-only SubActions that are never persisted to the database.
    /// DO NOT add [SubActionMetadata] attribute to prevent UI registration and database mapping.
    /// </summary>
    public abstract class RuntimeOnlySubActionType : SubActionType
    {
        // Runtime-only SubActions should NOT have the SubActionMetadata attribute
        // This prevents them from being registered in the UI and database
    }
}
