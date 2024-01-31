using Microsoft.Extensions.Caching.Memory;

namespace DotNetTwitchBot.Bot.TwitchServices
{
    public class ChatMessageIdTracker(IMemoryCache MessageIdCache)
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
