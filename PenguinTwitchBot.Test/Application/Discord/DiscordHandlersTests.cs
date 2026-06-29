using PenguinTwitchBot.Application.Discord;
using Xunit;

namespace PenguinTwitchBot.Test.Application.Discord
{
    public class DiscordConnectedNotificationTests
    {
        [Fact]
        public void TypeExists()
        {
            Assert.NotNull(typeof(DiscordConnectedNotification));
        }
    }

    public class DiscordReadyNotificationTests
    {
        [Fact]
        public void TypeExists()
        {
            Assert.NotNull(typeof(DiscordReadyNotification));
        }
    }

    public class DiscordConnectedHandlerTests
    {
        [Fact]
        public void Handle_MethodExists()
        {
            Assert.NotNull(typeof(DiscordConnectedHandler).GetMethod("Handle"));
        }
    }

    public class DiscordReadyHandlerTests
    {
        [Fact]
        public void Handle_MethodExists()
        {
            Assert.NotNull(typeof(DiscordReadyHandler).GetMethod("Handle"));
        }
    }
}