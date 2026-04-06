namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    /// <summary>
    /// Used to break out of a chain of subactions, this is used by the BreakType subaction, this should not be thrown manually as it will not be caught and will crash the application, it is only used internally to break out of a chain of subactions when a BreakType subaction is executed
    /// </summary>
    public class BreakException : Exception
    {
    }
}
