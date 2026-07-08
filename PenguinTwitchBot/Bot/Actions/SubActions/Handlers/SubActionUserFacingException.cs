using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class SubActionUserFacingException : SubActionHandlerException
    {
        public SubActionUserFacingException()
        {
        }

        public SubActionUserFacingException(string? message) : base(message)
        {
        }

        public SubActionUserFacingException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        public SubActionUserFacingException(
            SubActionType subActionType,
            string technicalMessage,
            string userFacingMessage,
            params object[] args)
            : base(subActionType, technicalMessage, args)
        {
            UserFacingMessage = SafeFormat(userFacingMessage, args);
        }

        public SubActionUserFacingException(
            SubActionType subActionType,
            Exception innerException,
            string technicalMessage,
            string userFacingMessage,
            params object[] args)
            : base(subActionType, innerException, technicalMessage, args)
        {
            UserFacingMessage = SafeFormat(userFacingMessage, args);
        }

        private static string SafeFormat(string message, object[] args)
        {
            if (args.Length == 0)
            {
                return message;
            }

            try
            {
                return string.Format(message, args);
            }
            catch
            {
                return message;
            }
        }
    }
}
