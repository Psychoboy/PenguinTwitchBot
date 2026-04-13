using DotNetTwitchBot.Bot.Models.Commands;
using System.Text.RegularExpressions;

namespace DotNetTwitchBot.Bot.Commands.Actions
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

    public class ActionKeywordCache : IActionKeywordCache
    {
        private readonly SemaphoreSlim _keywordLock = new(1, 1);
        private List<KeywordWithCompiledRegex> _cachedKeywords = new();
        private DateTime _lastCacheUpdate = DateTime.MinValue;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(1);
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
                // Reload cache if expired
                if (DateTime.UtcNow - _lastCacheUpdate > _cacheExpiration)
                {
                    await ReloadKeywordsCacheAsync();
                }

                return _cachedKeywords;
            }
            finally
            {
                _keywordLock.Release();
            }
        }

        private async Task ReloadKeywordsCacheAsync()
        {
            try
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var actionKeywordService = scope.ServiceProvider.GetRequiredService<IActionKeywordService>();

                var keywords = await actionKeywordService.GetAllEnabledAsync();
                _cachedKeywords = keywords.Select(k => new KeywordWithCompiledRegex(k)).ToList();

                _logger.LogInformation("Reloaded {Count} keywords into cache", _cachedKeywords.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reloading keywords cache");
            }
            finally
            {
                // Always update the timestamp, even on error, to prevent continuous reload attempts
                _lastCacheUpdate = DateTime.UtcNow;
            }
        }

        public void InvalidateCache()
        {
            _lastCacheUpdate = DateTime.MinValue;
            _logger.LogInformation("Keyword cache invalidated");
        }
    }
}
