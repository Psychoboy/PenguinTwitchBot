using DotNetTwitchBot.Bot.Actions.SubActions.Types;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class SubActionHandlerException : Exception
    {
        public SubActionType? SubActionType { get; }
        public object[] Args { get; } = [];
        public SubActionHandlerException(SubActionType subActionType) : base("An unknown error happened in a SubAction.") { SubActionType = subActionType; }
        public SubActionHandlerException(SubActionType subActionType, string message) : base(message) { SubActionType = subActionType; }
        public SubActionHandlerException(SubActionType subActionType ,string message, Exception innerException) : base(message, innerException) { SubActionType = subActionType; }
        public SubActionHandlerException(SubActionType subActionType, string message, params object[] args) :  base(string.Format(message, args)) { SubActionType = subActionType; }
        public SubActionHandlerException(SubActionType subActionType, Exception innerException, string message, params object[] args) : base(string.Format(message, args), innerException) { SubActionType = subActionType; }
    }
}
