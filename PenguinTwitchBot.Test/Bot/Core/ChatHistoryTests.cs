using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Models;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.CustomMiddleware;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
namespace PenguinTwitchBot.Test.Bot.Core
{
    public class ChatHistoryTests
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IServiceBackbone _serviceBackbone;
        private readonly ILogger<ChatHistory> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICommandHandler _commandHandler;
        private readonly ITwitchService _twitchService;
        private readonly IChatHistoryRetentionSettingsService _chatHistoryRetentionSettingsService;
        private readonly PenguinTwitchBot.Application.Notifications.IPenguinDispatcher dispatcherSubstitute;
        private readonly ChatHistory _chatHistory;

        public ChatHistoryTests()
        {
            _scopeFactory = Substitute.For<IServiceScopeFactory>();
            _serviceBackbone = Substitute.For<IServiceBackbone>();
            _logger = Substitute.For<ILogger<ChatHistory>>();
            _unitOfWork = Substitute.For<IUnitOfWork>();
            _commandHandler = Substitute.For<ICommandHandler>();
            _twitchService = Substitute.For<ITwitchService>();
            _chatHistoryRetentionSettingsService = Substitute.For<IChatHistoryRetentionSettingsService>();
            dispatcherSubstitute = Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>();

            var scope = Substitute.For<IServiceScope>();
            var serviceProvider = Substitute.For<IServiceProvider>();

            _scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(_unitOfWork);

            _chatHistory = new ChatHistory(_scopeFactory, _serviceBackbone, _commandHandler, _twitchService, _chatHistoryRetentionSettingsService, dispatcherSubstitute, _logger);
        }


        [Fact]
        public async Task AddChatMessage_ShouldAddMessage()
        {
            // Arrange
            var chatMessageEventArgs = new ChatMessageEventArgs { Name = "testUser", DisplayName = "Test User", Message = "Hello", FromOwnChannel=true };

            // Act
            await _chatHistory.AddChatMessage(chatMessageEventArgs);

            // Assert
            await _unitOfWork.ViewerChatHistories.Received(1).AddAsync(Arg.Any<ViewerChatHistory>());
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

    }
}