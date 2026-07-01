namespace PenguinTwitchBot.Bot.TwitchServices
{
    public interface IChatMessageIdTracker
    {
        bool IsSelfMessage(string messageId);
        void AddMessageId(string messageId);
    }
}
