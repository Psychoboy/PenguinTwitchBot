using PenguinTwitchBot.Application.ChatMessage.Notifications;
using PenguinTwitchBot.Application.Notifications;
using Xunit;

namespace PenguinTwitchBot.Test.Application.ChatMessage.Notifications
{
    public class ReceivedSuspiciousChatMessageTests
    {
        [Fact]
        public void TypeExists()
        {
            Assert.NotNull(typeof(ReceivedSuspiciousChatMessage));
        }

        [Fact]
        public void InheritsINotification()
        {
            var type = typeof(ReceivedSuspiciousChatMessage);
            var interfaces = type.GetInterfaces();
            Assert.Contains(typeof(INotification), interfaces);
        }
    }

    public class ReceivedChatMessageTests
    {
        [Fact]
        public void TypeExists()
        {
            Assert.NotNull(typeof(ReceivedChatMessage));
        }

        [Fact]
        public void InheritsINotification()
        {
            var type = typeof(ReceivedChatMessage);
            var interfaces = type.GetInterfaces();
            Assert.Contains(typeof(INotification), interfaces);
        }
    }
}