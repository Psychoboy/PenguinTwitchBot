using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
#pragma warning disable RCS1194 // Implement exception constructors
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

        public SubActionUserFacingException(SubActionType subActionType, string message) : base(subActionType, message, Array.Empty<object>())
        {
        }

        public SubActionUserFacingException(SubActionType subActionType, string message, params object[] args) : base(subActionType, message, args)
        {
        }
    }
    #pragma warning restore RCS1194 // Implement exception constructors
}
