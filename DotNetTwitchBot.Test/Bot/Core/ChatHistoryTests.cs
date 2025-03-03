using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models;
using DotNetTwitchBot.CustomMiddleware;
using DotNetTwitchBot.Repository;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
namespace DotNetTwitchBot.Test.Bot.Core
{
    public class ChatHistoryTests
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IServiceBackbone _serviceBackbone;
        private readonly ILogger<ChatHistory> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ChatHistory _chatHistory;

        public ChatHistoryTests()
        {
            _scopeFactory = Substitute.For<IServiceScopeFactory>();
            _serviceBackbone = Substitute.For<IServiceBackbone>();
            _logger = Substitute.For<ILogger<ChatHistory>>();
            _unitOfWork = Substitute.For<IUnitOfWork>();

            var scope = Substitute.For<IServiceScope>();
            var serviceProvider = Substitute.For<IServiceProvider>();

            _scopeFactory.CreateAsyncScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService<IUnitOfWork>().Returns(_unitOfWork);

            _chatHistory = new ChatHistory(_scopeFactory, _serviceBackbone, _logger);
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

        [Fact]
        public async Task CleanOldLogs_ShouldRemoveOldLogs()
        {
            // Arrange
            var oldLogs = new List<ViewerChatHistory>
            {
                new ViewerChatHistory { CreatedAt = DateTime.Now.AddMonths(-7) }
            };
            _unitOfWork.ViewerChatHistories.Find(Arg.Any<System.Linq.Expressions.Expression< Func<ViewerChatHistory, bool>>>()).Returns(oldLogs.AsQueryable());

            // Act
            await _chatHistory.CleanOldLogs();

            // Assert
            _unitOfWork.ViewerChatHistories.Received(1).RemoveRange(Arg.Any<IEnumerable<ViewerChatHistory>>());
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task StartAsync_ShouldSubscribeToCommandEvent()
        {
            // Act
            await _chatHistory.StartAsync(CancellationToken.None);

            // Assert
            _serviceBackbone.Received(1).CommandEvent += Arg.Any<AsyncEventHandler<CommandEventArgs>>();
        }

        [Fact]
        public async Task StopAsync_ShouldUnsubscribeFromCommandEvent()
        {
            // Act
            await _chatHistory.StopAsync(CancellationToken.None);

            // Assert
            _serviceBackbone.Received(1).CommandEvent -= Arg.Any<AsyncEventHandler<CommandEventArgs>>();
        }
    }
}