using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Services;
using PenguinTwitchBot.TwitchApi.Auth;
using PenguinTwitchBot.TwitchApi.Helix;
using PenguinTwitchBot.TwitchApi.Models.Chat;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.TwitchServices
{
    public class TwitchChatBotTests
    {
        private readonly ILogger<TwitchChatBot> _logger = Substitute.For<ILogger<TwitchChatBot>>();
        private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
        private readonly IAuthClient _authClient = Substitute.For<IAuthClient>();
        private readonly IConfiguration _configuration;
        private readonly ITwitchService _twitchService = Substitute.For<ITwitchService>();
        private readonly IChatMessageIdTracker _messageIdTracker = Substitute.For<IChatMessageIdTracker>();
        private readonly SettingsFileManager _settingsFileManager;

        public TwitchChatBotTests()
        {
            var configData = new Dictionary<string, string?>
            {
                ["twitchClientId"] = "test-client-id",
                ["twitchClientSecret"] = "test-client-secret",
                ["twitchBotAccessToken"] = "test-access-token"
            };
            _configuration = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();
            var sfmLogger = Substitute.For<ILogger<SettingsFileManager>>();
            _settingsFileManager = new SettingsFileManager(sfmLogger, _configuration);
        }

        private TwitchChatBot CreateBot()
        {
            var bot = new TwitchChatBot(
                _logger,
                _configuration,
                _twitchService,
                _authClient,
                _chatClient,
                _messageIdTracker,
                _settingsFileManager);
            bot.SetAccessToken("test-access-token");
            return bot;
        }

        [Fact]
        public async Task SendMessage_ReturnsNull_WhenMessageIsEmpty()
        {
            var bot = CreateBot();

            var result = await bot.SendMessage("");

            Assert.Null(result);
            await _chatClient.DidNotReceiveWithAnyArgs().SendChatMessageAsync(default!, default!, default!);
        }

        [Fact]
        public async Task SendMessage_ReturnsResult_WhenMessageSentSuccessfully()
        {
            var bot = CreateBot();
            _twitchService.GetBroadcasterUserId().Returns("broadcaster-123");
            _twitchService.GetBotUserId().Returns("bot-456");

            var expectedResult = new SendChatMessageResult("msg-1", true, null);
            var response = new SendChatMessageResponse([expectedResult]);

            _chatClient.SendChatMessageAsync("test-client-id", "test-access-token", Arg.Any<SendChatMessageRequest>())
                .Returns(response);

            var result = await bot.SendMessage("Hello chat!");

            Assert.NotNull(result);
            Assert.True(result.IsSent);
            Assert.Equal("msg-1", result.MessageId);
            _messageIdTracker.Received(1).AddMessageId("msg-1");
        }

        [Fact]
        public async Task SendMessage_ReturnsResult_WhenMessageDropped()
        {
            var bot = CreateBot();
            _twitchService.GetBroadcasterUserId().Returns("broadcaster-123");
            _twitchService.GetBotUserId().Returns("bot-456");

            var dropReason = new SendChatMessageDropReason("msg_banned", "You are banned from chat.");
            var expectedResult = new SendChatMessageResult("msg-2", false, dropReason);
            var response = new SendChatMessageResponse([expectedResult]);

            _chatClient.SendChatMessageAsync("test-client-id", "test-access-token", Arg.Any<SendChatMessageRequest>())
                .Returns(response);

            var result = await bot.SendMessage("Test dropped");

            Assert.NotNull(result);
            Assert.False(result.IsSent);
            Assert.Equal("msg_banned", result.DropReason?.Code);
        }

        [Fact]
        public async Task SendMessage_ReturnsNull_WhenSendThrowsException()
        {
            var bot = CreateBot();
            _twitchService.GetBroadcasterUserId().Returns("broadcaster-123");
            _twitchService.GetBotUserId().Returns("bot-456");

            _chatClient.SendChatMessageAsync("test-client-id", "test-access-token", Arg.Any<SendChatMessageRequest>())
                .ThrowsAsync(new HttpRequestException("Network failure"));

            var result = await bot.SendMessage("Will fail");

            Assert.Null(result);
        }
    }
}
