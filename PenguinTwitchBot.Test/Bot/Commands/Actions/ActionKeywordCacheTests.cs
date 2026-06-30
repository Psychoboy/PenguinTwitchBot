using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Commands.Actions;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Commands.Actions
{
    public class ActionKeywordCacheTests
    {
        [Fact]
        public void InvalidateCache_SetsLastCacheUpdateToMinValue()
        {
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var logger = Substitute.For<ILogger<ActionKeywordCache>>();

            var cache = new ActionKeywordCache(scopeFactory, logger);

            cache.InvalidateCache();

            var field = typeof(ActionKeywordCache).GetField("_lastCacheUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.Equal(DateTime.MinValue, field!.GetValue(cache)!);

            logger.Received().Log(
                LogLevel.Information,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                null,
                Arg.Any<Func<object, Exception?, string>>());
        }

        [Fact]
        public async Task GetKeywordsAsync_ReturnsCachedKeywords_WhenNotExpired()
        {
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var logger = Substitute.For<ILogger<ActionKeywordCache>>();

            var cache = new ActionKeywordCache(scopeFactory, logger);
            var keywordsField = typeof(ActionKeywordCache).GetField("_cachedKeywords", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            keywordsField!.SetValue(cache, new System.Collections.Generic.List<KeywordWithCompiledRegex> 
            { 
                new(new ActionKeyword { CommandName = "test" }) 
            });

            var timeField = typeof(ActionKeywordCache).GetField("_lastCacheUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            timeField!.SetValue(cache, DateTime.UtcNow);

            var result = await cache.GetKeywordsAsync();

            Assert.Single(result);
            Assert.Equal("test", result[0].Keyword.CommandName);
        }

        [Fact]
        public async Task GetKeywordsAsync_InvalidRegex_StillReturnsCache()
        {
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var logger = Substitute.For<ILogger<ActionKeywordCache>>();

            var cache = new ActionKeywordCache(scopeFactory, logger);
            var keyword = new ActionKeyword { CommandName = "[invalid", IsRegex = true };
            var keywordEntry = new KeywordWithCompiledRegex(keyword);

            var keywordsField = typeof(ActionKeywordCache).GetField("_cachedKeywords", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            keywordsField!.SetValue(cache, new System.Collections.Generic.List<KeywordWithCompiledRegex> { keywordEntry });

            var timeField = typeof(ActionKeywordCache).GetField("_lastCacheUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            timeField!.SetValue(cache, DateTime.UtcNow);

            var result = await cache.GetKeywordsAsync();

            Assert.Single(result);
            Assert.Equal("[invalid", result[0].Keyword.CommandName);
            Assert.Null(result[0].CompiledRegex);
        }
    }

    public class KeywordWithCompiledRegexTests
    {
        [Fact]
        public void Constructor_WithNonRegexKeyword_ShouldNotCompileRegex()
        {
            var keyword = new ActionKeyword { CommandName = "hello", IsRegex = false };
            var keywordEntry = new KeywordWithCompiledRegex(keyword);

            Assert.Null(keywordEntry.CompiledRegex);
        }

        [Fact]
        public void Constructor_WithValidRegexKeyword_ShouldCompileRegex()
        {
            var keyword = new ActionKeyword { CommandName = @"test\d+", IsRegex = true, IsCaseSensitive = true };
            var keywordEntry = new KeywordWithCompiledRegex(keyword);

            Assert.NotNull(keywordEntry.CompiledRegex);
            Assert.Matches(@"test\d+", "test123");
        }

        [Fact]
        public void Constructor_WithInvalidRegexKeyword_ShouldHaveNullCompiledRegex()
        {
            var keyword = new ActionKeyword { CommandName = "[invalid", IsRegex = true, IsCaseSensitive = false };
            var keywordEntry = new KeywordWithCompiledRegex(keyword);

            Assert.Null(keywordEntry.CompiledRegex);
        }

        [Fact]
        public void Constructor_WithRegexAndCaseSensitiveFalse_ShouldUseIgnoreCase()
        {
            var keyword = new ActionKeyword { CommandName = @"test\d+", IsRegex = true, IsCaseSensitive = false };
            var keywordEntry = new KeywordWithCompiledRegex(keyword);

            Assert.NotNull(keywordEntry.CompiledRegex);
            Assert.Matches(@"(?i)test\d+", "TEST12");
        }
    }
}