using PenguinTwitchBot.Application.Metrics.Handlers;
using System.Reflection;
using Xunit;

namespace PenguinTwitchBot.Test.Application.Metrics.Handlers
{
    public class IncreaseChatMessageCountHandlerTests
    {
        [Fact]
        public void Handle_MethodExists()
        {
            Assert.NotNull(typeof(IncreaseChatMessageCountHandler).GetMethod("Handle"));
        }
    }

    public class IncreaseNumberOfCommandsHandlerTests
    {
        [Fact]
        public void HasHandleMethods()
        {
            var methods = typeof(IncreaseNumberOfCommandsHandler).GetMethods(
                BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == "Handle");
            Assert.NotEmpty(methods);
        }
    }
}