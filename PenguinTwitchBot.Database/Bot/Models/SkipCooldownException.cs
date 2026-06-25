namespace PenguinTwitchBot.Bot.Models
{
    public class SkipCooldownException : Exception
    {
        public SkipCooldownException() { }
        public SkipCooldownException(string message) : base(message) { }
    }
}