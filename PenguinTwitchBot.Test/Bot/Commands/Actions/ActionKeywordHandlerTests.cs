using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Application.ChatMessage.Notifications;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Actions;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Commands.Actions
{
    public class ActionKeywordHandlerTests
    {
        private readonly IActionManagementService _actionManagement;
        private readonly IAction _actionService;
        private readonly ICommandHandler _commandHandler;
        private readonly IActionKeywordCache _keywordCache;
        private readonly ILogger<ActionKeywordHandler> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ActionKeywordHandler _handler;

        public ActionKeywordHandlerTests()
        {
            _actionManagement = Substitute.For<IActionManagementService>();
            _actionService = Substitute.For<IAction>();
            _commandHandler = Substitute.For<ICommandHandler>();
            _keywordCache = Substitute.For<IActionKeywordCache>();
            _logger = Substitute.For<ILogger<ActionKeywordHandler>>();

            var services = new ServiceCollection();
            services.AddSingleton(_actionManagement);
            services.AddSingleton(_actionService);
            services.AddSingleton(_commandHandler);
            services.AddSingleton(_keywordCache);
            services.AddSingleton(_logger);
            
            var provider = services.BuildServiceProvider();
            _scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

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
        public async Task Handle_MatchingNonRegexKeyword_CaseSensitive_Success()
        {
            var keyword = new ActionKeyword { CommandName = "hello", IsRegex = false, IsCaseSensitive = true, SourceOnly = false, SayCooldown = false };

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
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(Task.FromResult(true));
            _commandHandler.IsCoolDownExpired(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.FromResult(true));
            _actionManagement.GetActionsByTriggerTypeAndNameAsync(TriggerTypes.Keyword, keyword.CommandName)
                .Returns(Task.FromResult(new List<ActionType>()));

            await _handler.Handle(notification, CancellationToken.None);

            await _actionManagement.Received(1).GetActionsByTriggerTypeAndNameAsync(TriggerTypes.Keyword, keyword.CommandName);
            await _commandHandler.Received(1).CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>());
        }

        [Fact]
        public async Task Handle_MatchingNonRegexKeyword_CaseInsensitive_Success()
        {
            var keyword = new ActionKeyword { CommandName = "HELLO", IsRegex = false, IsCaseSensitive = false, SourceOnly = false, SayCooldown = false };

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
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(Task.FromResult(true));
            _commandHandler.IsCoolDownExpired(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.FromResult(true));
            _actionManagement.GetActionsByTriggerTypeAndNameAsync(TriggerTypes.Keyword, keyword.CommandName)
                .Returns(Task.FromResult(new List<ActionType>()));

            await _handler.Handle(notification, CancellationToken.None);

            await _actionManagement.Received(1).GetActionsByTriggerTypeAndNameAsync(TriggerTypes.Keyword, keyword.CommandName);
            await _commandHandler.Received(1).CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>());
        }

        [Fact]
        public async Task Handle_MatchingRegexKeyword_Success()
        {
            var keyword = new ActionKeyword 
            { 
                CommandName = "hello", 
                IsRegex = true, 
                IsCaseSensitive = true,
                SourceOnly = false,
                SayCooldown = false
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
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(Task.FromResult(true));
            _commandHandler.IsCoolDownExpired(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.FromResult(true));

            await _handler.Handle(notification, CancellationToken.None);
        }

        [Fact]
        public async Task Handle_InvalidRegexKeyword_SkipsKeyword()
        {
            var keyword = new ActionKeyword 
            { 
                CommandName = "[invalid", 
                IsRegex = true,
                SourceOnly = false,
                SayCooldown = false
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

            await _handler.Handle(notification, CancellationToken.None);
            
            await _commandHandler.DidNotReceive().CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>());
        }

        [Fact]
        public async Task Handle_SourceOnlyFailsSharedChatCheck_SkipsKeyword()
        {
            var keyword = new ActionKeyword { CommandName = "hello", IsRegex = false, SourceOnly = true };

            var notification = new ReceivedChatMessage
            {
                EventArgs = new ChatMessageEventArgs
                {
                    Message = "hello",
                    Name = "testuser",
                    DisplayName = "TestUser",
                    FromOwnChannel = false
                }
            };

            _keywordCache.GetKeywordsAsync().Returns(new List<KeywordWithCompiledRegex> { new(keyword) });

            await _handler.Handle(notification, CancellationToken.None);
            
            await _commandHandler.DidNotReceive().CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>());
        }

        [Fact]
        public async Task Handle_PermissionFails_SkipsKeyword()
        {
            var keyword = new ActionKeyword { CommandName = "hello", IsRegex = false, SourceOnly = false, SayCooldown = false };

            var notification = new ReceivedChatMessage
            {
                EventArgs = new ChatMessageEventArgs
                {
                    Message = "hello",
                    Name = "testuser",
                    DisplayName = "TestUser",
                    FromOwnChannel = true
                }
            };

            _keywordCache.GetKeywordsAsync().Returns(new List<KeywordWithCompiledRegex> { new(keyword) });
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(Task.FromResult(false));
            _commandHandler.IsCoolDownExpired(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.FromResult(true));

            await _handler.Handle(notification, CancellationToken.None);
            
            await _commandHandler.DidNotReceive().AddCoolDown(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
        }

        [Fact]
        public async Task Handle_SayCooldownTrue_PassesGlobalCooldownCheck()
        {
            var keyword = new ActionKeyword { CommandName = "hello", IsRegex = false, SayCooldown = true, SourceOnly = false };

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
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(Task.FromResult(true));
            _commandHandler.IsGlobalCoolDownExpiredWithMessageForAction(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(Task.FromResult(true));

            await _handler.Handle(notification, CancellationToken.None);
        }

        [Fact]
        public async Task Handle_SayCooldownFalse_PassesUserCooldownCheck()
        {
            var keyword = new ActionKeyword { CommandName = "hello", IsRegex = false, SayCooldown = false, SourceOnly = false };

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
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(Task.FromResult(true));
            _commandHandler.IsCoolDownExpired(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.FromResult(true));

            await _handler.Handle(notification, CancellationToken.None);
        }

        [Fact]
        public async Task Handle_RegexTimeout_LogsWarning()
        {
            var keyword = new ActionKeyword 
            { 
                CommandName = @"(a+)+b", 
                IsRegex = true,
                SourceOnly = false,
                SayCooldown = false
            };
            var regex = new Regex(@"(a+)+b", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(500));

            var notification = new ReceivedChatMessage
            {
                EventArgs = new ChatMessageEventArgs
                {
                    Message = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    Name = "testuser",
                    FromOwnChannel = true
                }
            };

            _keywordCache.GetKeywordsAsync().Returns(new List<KeywordWithCompiledRegex> { new(keyword) { CompiledRegex = regex } });
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(Task.FromResult(true));
            _commandHandler.IsCoolDownExpired(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.FromResult(true));

            await _handler.Handle(notification, CancellationToken.None);

            _logger.Received(1).Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(v => v!.ToString()!.Contains("Regex timeout")),
                null,
                Arg.Any<Func<object, Exception?, string>>());

            await _actionManagement.DidNotReceive().GetActionsByTriggerTypeAndNameAsync(Arg.Any<TriggerTypes>(), Arg.Any<string>());
            await _actionService.DidNotReceive().EnqueueAction(Arg.Any<ConcurrentDictionary<string, string>>(), Arg.Any<ActionType>());
        }

        [Fact]
        public async Task Handle_MatchingKeyword_EnqueuesAction()
        {
            var keyword = new ActionKeyword { CommandName = "hello", IsRegex = false, SourceOnly = false, SayCooldown = false };
            var action = new ActionType { Name = "TestAction", SubActions = new List<SubActionType>() };
            _actionManagement.GetActionsByTriggerTypeAndNameAsync(TriggerTypes.Keyword, "hello")
                .Returns(Task.FromResult(new List<ActionType> { action }));
            
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
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(Task.FromResult(true));
            _commandHandler.IsCoolDownExpired(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.FromResult(true));

            await _handler.Handle(notification, CancellationToken.None);

            await _actionService.Received(1).EnqueueAction(Arg.Any<ConcurrentDictionary<string, string>>(), action);
        }

        [Fact]
        public async Task Handle_MatchingKeyword_WithGlobalCooldown_SetsCooldown()
        {
            var keyword = new ActionKeyword 
            { 
                CommandName = "hello", 
                IsRegex = false, 
                SourceOnly = false,
                SayCooldown = false,
                GlobalCooldown = 30,
                GlobalCooldownMax = 60
            };
            _actionManagement.GetActionsByTriggerTypeAndNameAsync(TriggerTypes.Keyword, "hello")
                .Returns(Task.FromResult(new List<ActionType>()));
            
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
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(Task.FromResult(true));
            _commandHandler.IsCoolDownExpired(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.FromResult(true));

            await _handler.Handle(notification, CancellationToken.None);

            await _commandHandler.Received(1).AddGlobalCooldown($"keyword {keyword.CommandName}", Arg.Is<int>(v => v >= 30 && v <= 60));
        }

        [Fact]
        public async Task Handle_MatchingKeyword_WithUserCooldown_SetsCooldown()
        {
            var keyword = new ActionKeyword 
            { 
                CommandName = "hello", 
                IsRegex = false, 
                SourceOnly = false,
                SayCooldown = false,
                UserCooldown = 30,
                UserCooldownMax = 60
            };
            _actionManagement.GetActionsByTriggerTypeAndNameAsync(TriggerTypes.Keyword, "hello")
                .Returns(Task.FromResult(new List<ActionType>()));
            
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
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(Task.FromResult(true));
            _commandHandler.IsCoolDownExpired(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.FromResult(true));

            await _handler.Handle(notification, CancellationToken.None);

            await _commandHandler.Received(1).AddCoolDown(
                "testuser",
                $"keyword {keyword.CommandName}",
                Arg.Is<int>(v => v >= 30 && v <= 60));
        }

        [Fact]
        public async Task Handle_MultipleActions_EnqueuesAll()
        {
            var keyword = new ActionKeyword { CommandName = "hello", IsRegex = false, SourceOnly = false, SayCooldown = false };
            var action1 = new ActionType { Name = "Action1", SubActions = new List<SubActionType>() };
            var action2 = new ActionType { Name = "Action2", SubActions = new List<SubActionType>() };
            _actionManagement.GetActionsByTriggerTypeAndNameAsync(TriggerTypes.Keyword, "hello")
                .Returns(Task.FromResult(new List<ActionType> { action1, action2 }));
            
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
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(Task.FromResult(true));
            _commandHandler.IsCoolDownExpired(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.FromResult(true));

            await _handler.Handle(notification, CancellationToken.None);

            await _actionService.Received(2).EnqueueAction(Arg.Any<ConcurrentDictionary<string, string>>(), Arg.Any<ActionType>());
        }

        [Fact]
        public async Task Handle_Exception_LogsError()
        {
            var keyword = new ActionKeyword { CommandName = "hello", IsRegex = false, SourceOnly = false, SayCooldown = false };
            var action = new ActionType { Name = "TestAction", SubActions = new List<SubActionType>() };
            _actionManagement.GetActionsByTriggerTypeAndNameAsync(TriggerTypes.Keyword, "hello")
                .Returns(Task.FromResult(new List<ActionType> { action }));
            
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
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(Task.FromResult(true));
            _commandHandler.IsCoolDownExpired(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.FromResult(true));
            _actionService.EnqueueAction(Arg.Any<ConcurrentDictionary<string, string>>(), Arg.Any<ActionType>())
                .Returns(Task.FromException(new Exception("Test exception")));

            var exception = await Record.ExceptionAsync(() => _handler.Handle(notification, CancellationToken.None));

            Assert.Null(exception);

            _logger.Received(1).Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(v => v!.ToString()!.Contains("Error handling keyword triggers")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());
        }

        [Fact]
        public async Task Handle_SayCooldownTrue_WithGlobalCooldown_SetsBothCooldowns()
        {
            var keyword = new ActionKeyword 
            { 
                CommandName = "hello", 
                IsRegex = false, 
                SayCooldown = true,
                SourceOnly = false,
                GlobalCooldown = 10,
                GlobalCooldownMax = 20,
                UserCooldown = 5,
                UserCooldownMax = 10
            };
            _actionManagement.GetActionsByTriggerTypeAndNameAsync(TriggerTypes.Keyword, "hello")
                .Returns(Task.FromResult(new List<ActionType>()));
            
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
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(Task.FromResult(true));
            _commandHandler.IsGlobalCoolDownExpiredWithMessageForAction(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(Task.FromResult(true));

            await _handler.Handle(notification, CancellationToken.None);

            await _commandHandler.Received(1).AddGlobalCooldown($"keyword {keyword.CommandName}", Arg.Is<int>(v => v >= 10 && v <= 20));
            await _commandHandler.Received(1).AddCoolDown(
                "testuser",
                $"keyword {keyword.CommandName}",
                Arg.Is<int>(v => v >= 5 && v <= 10));
        }

        [Fact]
        public async Task Handle_SayCooldownGlobalCooldownCheckFailed_SkipsKeyword()
        {
            var keyword = new ActionKeyword { CommandName = "hello", IsRegex = false, SayCooldown = true, SourceOnly = false };

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
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(Task.FromResult(true));
            _commandHandler.IsGlobalCoolDownExpiredWithMessageForAction(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(Task.FromResult(false));

            await _handler.Handle(notification, CancellationToken.None);

            await _actionManagement.DidNotReceive().GetActionsByTriggerTypeAndNameAsync(Arg.Any<TriggerTypes>(), Arg.Any<string>());
            await _actionService.DidNotReceive().EnqueueAction(Arg.Any<ConcurrentDictionary<string, string>>(), Arg.Any<ActionType>());
            await _commandHandler.DidNotReceive().AddGlobalCooldown(Arg.Any<string>(), Arg.Any<int>());
            await _commandHandler.DidNotReceive().AddCoolDown(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
        }

        [Fact]
        public async Task Handle_SayCooldownFalse_UserCooldownCheckFailed_SkipsKeyword()
        {
            var keyword = new ActionKeyword { CommandName = "hello", IsRegex = false, SayCooldown = false, SourceOnly = false };

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
            _commandHandler.CheckPermission(Arg.Any<BaseCommandProperties>(), Arg.Any<CommandEventArgs>()).Returns(Task.FromResult(true));
            _commandHandler.IsCoolDownExpired(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.FromResult(false));

            await _handler.Handle(notification, CancellationToken.None);

            await _actionManagement.DidNotReceive().GetActionsByTriggerTypeAndNameAsync(Arg.Any<TriggerTypes>(), Arg.Any<string>());
            await _actionService.DidNotReceive().EnqueueAction(Arg.Any<ConcurrentDictionary<string, string>>(), Arg.Any<ActionType>());
            await _commandHandler.DidNotReceive().AddGlobalCooldown(Arg.Any<string>(), Arg.Any<int>());
            await _commandHandler.DidNotReceive().AddCoolDown(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
        }
    }
}