using OpenAI;

namespace PenguinTwitchBot.Bot.Ai
{
    public class OpenAiModelService : IOpenAiModelService
    {
        private readonly OpenAIClient? _client;
        private readonly ILogger<OpenAiModelService> _logger;
        private readonly SemaphoreSlim _cacheLock = new(1, 1);
        private IReadOnlyList<string>? _cachedModels;
        private DateTime _cacheExpiresAt = DateTime.MinValue;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
        private static readonly TimeSpan FallbackCacheDuration = TimeSpan.FromMinutes(1);

        public static readonly string[] DefaultTextModels =
        [
            "gpt-5.1",
            "gpt-4.1",
            "gpt-4.1-mini",
            "gpt-4.1-nano",
            "gpt-4o",
            "gpt-4o-mini",
            "o3-mini",
            "o1"
        ];

        public OpenAiModelService(ILogger<OpenAiModelService> logger, OpenAIClient? client = null)
        {
            _logger = logger;
            _client = client;
        }

        public IReadOnlyList<string>? CachedModels
        {
            get
            {
                if (_client == null)
                {
                    return DefaultTextModels;
                }

                if (_cachedModels != null && DateTime.UtcNow < _cacheExpiresAt)
                {
                    return _cachedModels;
                }
                return null;
            }
        }

        public async Task<IReadOnlyList<string>> GetAvailableTextModelsAsync(CancellationToken cancellationToken = default)
        {
            if (_cachedModels != null && DateTime.UtcNow < _cacheExpiresAt)
            {
                return _cachedModels;
            }

            if (_client == null)
            {
                _cachedModels = DefaultTextModels;
                _cacheExpiresAt = DateTime.UtcNow.Add(CacheDuration);
                return _cachedModels;
            }

            await _cacheLock.WaitAsync(cancellationToken);
            try
            {
                if (_cachedModels != null && DateTime.UtcNow < _cacheExpiresAt)
                {
                    return _cachedModels;
                }

                var modelClient = _client.GetOpenAIModelClient();
                var result = await modelClient.GetModelsAsync(cancellationToken);

                var textModels = result.Value
                    .Select(m => m.Id)
                    .Where(IsTextModel)
                    .OrderBy(m => m)
                    .ToList();

                if (textModels.Count > 0)
                {
                    _cachedModels = textModels.AsReadOnly();
                    _cacheExpiresAt = DateTime.UtcNow.Add(CacheDuration);
                    return _cachedModels;
                }

                _cachedModels = DefaultTextModels;
                _cacheExpiresAt = DateTime.UtcNow.Add(FallbackCacheDuration);
                return _cachedModels;
            }
            catch (Exception ex)
            {
                _cachedModels = DefaultTextModels;
                _cacheExpiresAt = DateTime.UtcNow.Add(FallbackCacheDuration);
                _logger.LogWarning(ex, "Failed to query OpenAI models from API. Falling back to default text models.");
                return _cachedModels;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public static bool IsTextModel(string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId))
                return false;

            var id = modelId.ToLowerInvariant();

            // Exclude non-text/specialized models
            if (id.Contains("embedding") ||
                id.Contains("dall-e") ||
                id.Contains("tts") ||
                id.Contains("whisper") ||
                id.Contains("moderation") ||
                id.Contains("realtime") ||
                id.Contains("audio") ||
                id.Contains("transcription") ||
                id.Contains("translation"))
            {
                return false;
            }

            // Include chat / reasoning / text models
            return id.StartsWith("gpt-") ||
                   id.StartsWith("o1") ||
                   id.StartsWith("o3") ||
                   id.StartsWith("chatgpt-");
        }
    }
}

