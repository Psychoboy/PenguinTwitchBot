using Microsoft.Extensions.Caching.Memory;

namespace DotNetTwitchBot.Bot.TwitchServices
{
    public class ChatMessageIdTracker(IMemoryCache MessageIdCache)
    {
        private readonly MemoryCacheEntryOptions _memoryCachOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(10));

        public void AddMessageId(string messageId)
        {
            MessageIdCache.Set(messageId, messageId);
        }

        public bool IsSelfMessage(string messageId)
        {
            return MessageIdCache.TryGetValue(messageId, out _);
        }
    }
}
