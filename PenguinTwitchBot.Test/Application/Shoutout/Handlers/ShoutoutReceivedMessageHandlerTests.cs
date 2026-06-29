using PenguinTwitchBot.Application.Shoutout.Handlers;
using Xunit;

namespace PenguinTwitchBot.Test.Application.Shoutout.Handlers
{
    public class ShoutoutReceivedMessageHandlerTests
    {
        [Fact]
        public void Handle_MethodExists()
        {
            Assert.NotNull(typeof(ShoutoutReceivedMessageHandler).GetMethod("Handle"));
        }
    }
}