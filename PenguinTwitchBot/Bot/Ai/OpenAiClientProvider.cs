using OpenAI;

namespace PenguinTwitchBot.Bot.Ai
{
    public interface IOpenAiClientProvider
    {
        /// <summary>Client for the currently configured API key, or null when no key is set.</summary>
        OpenAIClient? Client { get; }
    }

    public class OpenAiClientProvider(IConfiguration configuration) : IOpenAiClientProvider
    {
        private readonly object _lock = new();
        private string? _apiKey;
        private OpenAIClient? _client;

        public OpenAIClient? Client
        {
            get
            {
                var key = configuration["OpenAI:ApiKey"];
                if (string.IsNullOrWhiteSpace(key)) key = null;

                lock (_lock)
                {
                    if (key != _apiKey)
                    {
                        _apiKey = key;
                        _client = key == null ? null : new OpenAIClient(key);
                    }
                    return _client;
                }
            }
        }
    }
}
