using DotNetTwitchBot.Bot.Actions.SubActions.Handlers;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Queues;
using Moq;
using System.Diagnostics;

namespace DotNetTwitchBot.Test.Bot.Actions.SubActions
{
    public class DelayHandlerTests
    {
        [Fact]
        public async Task ValidDelayType_DelaysExecution()
        {
            // Arrange
            var mockContextAccessor = new Mock<ISubActionExecutionContextAccessor>();
            var handler = new DelayHandler(mockContextAccessor.Object);

            var delayType = new DelayType
            {
                Duration = "100" // 100ms delay
            };

            var variables = new Dictionary<string, string>();
            var stopwatch = Stopwatch.StartNew();

            // Act
            await handler.ExecuteAsync(delayType, variables);
            stopwatch.Stop();

            // Assert
            Assert.True(stopwatch.ElapsedMilliseconds >= 95, $"Expected delay >= 100ms, but was {stopwatch.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task DelayType_WithVariableReplacement_DelaysCorrectly()
        {
            // Arrange
            var mockContextAccessor = new Mock<ISubActionExecutionContextAccessor>();
            var handler = new DelayHandler(mockContextAccessor.Object);

            var delayType = new DelayType
            {
                Duration = "%delay_time%"
            };

            var variables = new Dictionary<string, string> { { "delay_time", "50" } };
            var stopwatch = Stopwatch.StartNew();

            // Act
            await handler.ExecuteAsync(delayType, variables);
            stopwatch.Stop();

            // Assert
            Assert.True(stopwatch.ElapsedMilliseconds >= 50, $"Expected delay >= 50ms, but was {stopwatch.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task DelayType_WithInvalidDuration_DoesNotDelay()
        {
            // Arrange
            var mockContextAccessor = new Mock<ISubActionExecutionContextAccessor>();
            var handler = new DelayHandler(mockContextAccessor.Object);

            var delayType = new DelayType
            {
                Duration = "invalid"
            };

            var variables = new Dictionary<string, string>();
            var stopwatch = Stopwatch.StartNew();

            // Act
            await handler.ExecuteAsync(delayType, variables);
            stopwatch.Stop();

            // Assert
            // Should complete quickly without delay
            Assert.True(stopwatch.ElapsedMilliseconds < 50);
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            // Arrange
            var mockContextAccessor = new Mock<ISubActionExecutionContextAccessor>();
            var handler = new DelayHandler(mockContextAccessor.Object);

            var wrongType = new SendMessageType();
            var variables = new Dictionary<string, string>();

            // Act & Assert
            await Assert.ThrowsAnyAsync<Exception>(
                () => handler.ExecuteAsync(wrongType, variables));
        }
    }
}
