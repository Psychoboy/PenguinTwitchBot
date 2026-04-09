using DotNetTwitchBot.Bot.Actions.SubActions.Types;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class SubActionHandlerException : Exception
    {
        public SubActionType? SubActionType { get; }
        public object[] Args { get; }
        public SubActionHandlerException(SubActionType subActionType) : base("An unknown error happened in a SubAction.") { SubActionType = subActionType; Args = []; }
        public SubActionHandlerException(SubActionType subActionType, string message) : base(message) { SubActionType = subActionType; Args = []; }
        public SubActionHandlerException(SubActionType subActionType ,string message, Exception innerException) : base(message, innerException) { SubActionType = subActionType; Args = []; }
        public SubActionHandlerException(SubActionType subActionType, string message, params object[] args) :  base(SafeFormat(message, args)) { SubActionType = subActionType; Args = args ?? []; }
        public SubActionHandlerException(SubActionType subActionType, Exception innerException, string message, params object[] args) : base(SafeFormat(message, args), innerException) { SubActionType = subActionType; Args = args ?? []; }

        private static string SafeFormat(string message, object[] args)
        {
            if (args == null || args.Length == 0)
                return message;

            try
            {
                return string.Format(message, args);
            }
            catch
            {
                return $"{message} [Args: {string.Join(", ", args)}]";
            }
        }
    }
}
