namespace PenguinTwitchBot.Database.Bot.Models
{
    public class SkipCooldownException : Exception
    {
        public SkipCooldownException() { }
        public SkipCooldownException(string message) : base(message) { }

        public SkipCooldownException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}