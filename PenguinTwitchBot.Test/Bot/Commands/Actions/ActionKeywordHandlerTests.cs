using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Application.ChatMessage.Notifications;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Actions;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Commands.Actions
{
    public class ActionKeywordHandlerTests
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ICommandHandler _commandHandler;
        private readonly IActionKeywordCache _keywordCache;
        private readonly ILogger<ActionKeywordHandler> _logger;
        private readonly ActionKeywordHandler _handler;

        public ActionKeywordHandlerTests()
        {
            _scopeFactory = Substitute.For<IServiceScopeFactory>();
            _commandHandler = Substitute.For<ICommandHandler>();
            _keywordCache = Substitute.For<IActionKeywordCache>();
            _logger = Substitute.For<ILogger<ActionKeywordHandler>>();
            _handler = new ActionKeywordHandler(_scopeFactory, _commandHandler, _keywordCache, _logger);
        }

        [Fact]
        public async Task Handle_NullEventArgs_ReturnsEarly()
        {
            var notification = new ReceivedChatMessage
            {
                EventArgs = null!
            };

            await _handler.Handle(notification, CancellationToken.None);

            await _keywordCache.DidNotReceive().GetKeywordsAsync();
        }

        [Fact]
        public async Task Handle_WhitespaceMessage_ReturnsEarly()
        {
            var notification = new ReceivedChatMessage
            {
                EventArgs = new ChatMessageEventArgs
                {
                    Message = "   ",
                    Name = "testuser"
                }
            };

            await _handler.Handle(notification, CancellationToken.None);

            await _keywordCache.DidNotReceive().GetKeywordsAsync();
        }

        [Fact]
        public async Task Handle_CommandMessage_ReturnsEarly()
        {
            var notification = new ReceivedChatMessage
            {
                EventArgs = new ChatMessageEventArgs
                {
                    Message = "!hello",
                    Name = "testuser"
                }
            };

            await _handler.Handle(notification, CancellationToken.None);

            await _keywordCache.DidNotReceive().GetKeywordsAsync();
        }

        [Fact]
        public async Task Handle_NoMatchingKeywords_DoesNotCheckPermissions()
        {
            var notification = new ReceivedChatMessage
            {
                EventArgs = new ChatMessageEventArgs
                {
                    Message = "hello world",
                    Name = "testuser"
                }
            };
            _keywordCache.GetKeywordsAsync().Returns(new List<KeywordWithCompiledRegex>());

            await _handler.Handle(notification, CancellationToken.None);

            await _commandHandler.DidNotReceive().CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>());
        }

        [Fact]
        public async Task Handle_MatchingNonRegexKeyword_Success()
        {
            var keyword = new ActionKeyword { CommandName = "hello", IsRegex = false, IsCaseSensitive = true };

            var notification = new ReceivedChatMessage
            {
                EventArgs = new ChatMessageEventArgs
                {
                    Message = "helloworld",
                    Name = "testuser",
                    FromOwnChannel = true
                }
            };

            _keywordCache.GetKeywordsAsync().Returns(new List<KeywordWithCompiledRegex> { new(keyword) });
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(true);
            _commandHandler.IsCoolDownExpired(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

            await _handler.Handle(notification, CancellationToken.None);
        }

        [Fact]
        public async Task Handle_MatchingRegexKeyword_Success()
        {
            var keyword = new ActionKeyword 
            { 
                CommandName = "hello", 
                IsRegex = true, 
                IsCaseSensitive = true 
            };
            var regex = new Regex("hello", RegexOptions.Compiled);

            var notification = new ReceivedChatMessage
            {
                EventArgs = new ChatMessageEventArgs
                {
                    Message = "helloworld",
                    Name = "testuser",
                    FromOwnChannel = true
                }
            };

            _keywordCache.GetKeywordsAsync().Returns(new List<KeywordWithCompiledRegex> { new(keyword) { CompiledRegex = regex } });

            await _handler.Handle(notification, CancellationToken.None);
        }

        [Fact]
        public async Task Handle_InvalidRegexKeyword_SkipsKeyword()
        {
            var keyword = new ActionKeyword 
            { 
                CommandName = "[invalid", 
                IsRegex = true 
            };

            var notification = new ReceivedChatMessage
            {
                EventArgs = new ChatMessageEventArgs
                {
                    Message = "hello",
                    Name = "testuser",
                    FromOwnChannel = true
                }
            };

            _keywordCache.GetKeywordsAsync().Returns(new List<KeywordWithCompiledRegex> { new(keyword) { CompiledRegex = null } });
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(true);
            _commandHandler.IsCoolDownExpired(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

            await _handler.Handle(notification, CancellationToken.None);
            
            await _commandHandler.DidNotReceive().CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>());
        }

        [Fact]
        public async Task Handle_SayCooldownFalse_UsesUserCooldown()
        {
            var keyword = new ActionKeyword { CommandName = "hello", IsRegex = false, SayCooldown = false };

            var notification = new ReceivedChatMessage
            {
                EventArgs = new ChatMessageEventArgs
                {
                    Message = "hello",
                    Name = "testuser",
                    FromOwnChannel = true
                }
            };

            _keywordCache.GetKeywordsAsync().Returns(new List<KeywordWithCompiledRegex> { new(keyword) });
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(true);
            _commandHandler.IsCoolDownExpired(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

            await _handler.Handle(notification, CancellationToken.None);
        }
    }
}