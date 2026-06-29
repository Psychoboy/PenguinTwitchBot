using PenguinTwitchBot.Application.Blacklist.Handlers;
using PenguinTwitchBot.Application.ChatMessage.Notifications;
using Xunit;

namespace PenguinTwitchBot.Test.Application.Blacklist.Handlers
{
    public class BlacklistChatReceivedHandlerTests
    {
        [Fact]
        public void Handle_MethodExists()
        {
            Assert.NotNull(typeof(BlacklistChatReceivedHandler).GetMethod("Handle"));
        }
    }

    public class BlacklistSuspiciousChatReceivedHandlerTests
    {
        [Fact]
        public void Handle_MethodExists()
        {
            Assert.NotNull(typeof(BlacklistSuspiciousChatReceivedHandler).GetMethod("Handle"));
        }
    }

    public class BlacklistSuspiciousHandlerTests
    {
        [Fact]
        public void Handle_MethodExists()
        {
            Assert.NotNull(typeof(BlacklistSuspiciousHandler).GetMethod("Handle"));
        }
    }
}