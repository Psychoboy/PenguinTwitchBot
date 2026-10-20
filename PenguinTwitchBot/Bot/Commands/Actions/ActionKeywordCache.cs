using PenguinTwitchBot.Database.Bot.Models.Commands;
using System.Text.RegularExpressions;

namespace PenguinTwitchBot.Bot.Commands.Actions
{
    public interface IActionKeywordCache
    {
        Task<List<KeywordWithCompiledRegex>> GetKeywordsAsync();
        void InvalidateCache();
    }

    public class KeywordWithCompiledRegex
    {
        public ActionKeyword Keyword { get; set; } = null!;
        public Regex? CompiledRegex { get; set; }

        public KeywordWithCompiledRegex(ActionKeyword keyword)
        {
            Keyword = keyword;
            if (keyword.IsRegex)
            {
                try
                {
                    CompiledRegex = new Regex(
                        keyword.CommandName,
                        keyword.IsCaseSensitive ? RegexOptions.Compiled : RegexOptions.Compiled | RegexOptions.IgnoreCase,
                        TimeSpan.FromMilliseconds(500));
                }
                catch (Exception)
                {
                    // Invalid regex, will be null
                    CompiledRegex = null;
                }
            }
        }
    }

    public class ActionKeywordCache : IActionKeywordCache, IHostedService
    {
        private readonly SemaphoreSlim _keywordLock = new(1, 1);
        private List<KeywordWithCompiledRegex> _cachedKeywords = new();
        private long _cacheVersion;
        private long _loadedVersion = -1;
        private long _failedVersion = -1;
        private DateTime _retryAfterUtc = DateTime.MinValue;
        private readonly TimeSpan _loadFailureRetryDelay = TimeSpan.FromMinutes(1);
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<ActionKeywordCache> _logger;

        public ActionKeywordCache(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<ActionKeywordCache> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        public async Task<List<KeywordWithCompiledRegex>> GetKeywordsAsync()
        {
            await _keywordLock.WaitAsync();
            try
            {
                var cacheVersion = Volatile.Read(ref _cacheVersion);
                if (_loadedVersion != cacheVersion &&
                    (_failedVersion != cacheVersion || DateTime.UtcNow >= _retryAfterUtc))
                {
                    await ReloadKeywordsCacheAsync(cacheVersion);
                }

                return _cachedKeywords;
            }
            finally
            {
                _keywordLock.Release();
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await GetKeywordsAsync();
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private async Task ReloadKeywordsCacheAsync(long cacheVersion)
        {
            try
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var actionKeywordService = scope.ServiceProvider.GetRequiredService<IActionKeywordService>();

                var keywords = await actionKeywordService.GetAllEnabledAsync();
                _cachedKeywords = keywords.Select(k => new KeywordWithCompiledRegex(k)).ToList();
                _loadedVersion = cacheVersion;
                _failedVersion = -1;

                _logger.LogInformation("Reloaded {Count} keywords into cache", _cachedKeywords.Count);
            }
            catch (Exception ex)
            {
                _failedVersion = cacheVersion;
                _retryAfterUtc = DateTime.UtcNow + _loadFailureRetryDelay;
                _logger.LogError(ex, "Error reloading keywords cache");
            }
        }

        public void InvalidateCache()
        {
            Interlocked.Increment(ref _cacheVersion);
            _logger.LogInformation("Keyword cache invalidated");
        }
    }
}
