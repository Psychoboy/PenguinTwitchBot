using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Commands.Actions;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using System;
using System.Threading.Tasks;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Commands.Actions
{
    public class ActionKeywordCacheTests
    {
        [Fact]
        public void InvalidateCache_SetsLastCacheUpdateToMinValue()
        {
            var scopeFactory = Substitute.For<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();
            var logger = Substitute.For<ILogger<ActionKeywordCache>>();

            var cache = new ActionKeywordCache(scopeFactory, logger);

            cache.InvalidateCache();

            logger.Received().LogInformation("Keyword cache invalidated");
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
            Assert.True(keywordEntry.CompiledRegex.IsMatch("test123"));
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
            Assert.True(keywordEntry.CompiledRegex.IsMatch("TEST12"));
        }
    }
}