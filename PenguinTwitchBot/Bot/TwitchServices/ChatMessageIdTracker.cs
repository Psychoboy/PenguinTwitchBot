using Microsoft.Extensions.Caching.Memory;

namespace PenguinTwitchBot.Bot.TwitchServices
{
    public class ChatMessageIdTracker(IMemoryCache MessageIdCache) : IChatMessageIdTracker
    {
        public void AddMessageId(string messageId)
        {
            MessageIdCache.Set(messageId, messageId, TimeSpan.FromMinutes(10));
        }

        public bool IsSelfMessage(string messageId)
        {
            return MessageIdCache.TryGetValue(messageId, out _);
        }
    }
}
