using Xunit;
using Moq;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using PenguinTwitchBot.Bot.Hubs;
using PenguinTwitchBot.Services;

namespace PenguinTwitchBot.Test.Bot.Hubs
{
    public class SignalRHubConnectionWrapperTests
    {
        /// <summary>
        /// Creates a real HubConnection using the official framework builder pattern.
        /// This cleanly bypasses internal parameter constructor mismatches.
        /// </summary>
        private HubConnection CreateTestHubConnection()
        {
            return new HubConnectionBuilder()
                .WithUrl("http://localhost/dummyHub") // Dummy location endpoint to satisfy configuration requirements
                .Build();
        }

        [Fact]
        public void Constructor_WithNullConnection_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new SignalRHubConnectionWrapper(null!));
            Assert.Equal("connection", exception.ParamName);
        }

        [Fact]
        public void Constructor_WithValidConnection_ShouldInitializeSuccessfully()
        {
            // Arrange
            var connection = CreateTestHubConnection();

            // Act
            var wrapper = new SignalRHubConnectionWrapper(connection);

            // Assert
            Assert.NotNull(wrapper);
        }

        [Fact]
        public void On_ShouldRegisterHandlerWithoutThrowing()
        {
            // Arrange
            var connection = CreateTestHubConnection();
            var wrapper = new SignalRHubConnectionWrapper(connection);
            var eventName = "TestEvent";
            Func<string, Task> dummyHandler = (msg) => Task.CompletedTask;

            // Act & Assert
            var exception = Record.Exception(() => wrapper.On<string>(eventName, dummyHandler));
            Assert.Null(exception);
        }

        [Fact]
        public async Task StartAsync_ShouldInvokeUnderlyingStartSequence()
        {
            // Arrange
            var connection = CreateTestHubConnection();
            var wrapper = new SignalRHubConnectionWrapper(connection);

            // Act & Assert
            // Since there's no actual running host at our dummy address, StartAsync executes 
            // the wrapper block entirely and fails on transport fallback negotiation loops.
            // Catching this specific exception provides 100% statement execution validation.
            await Assert.ThrowsAsync<System.Net.Http.HttpRequestException>(async () =>
            {
                await wrapper.StartAsync();
            });
        }

        [Fact]
        public async Task DisposeAsync_ShouldTeardownConnectionCleanly()
        {
            // Arrange
            var connection = CreateTestHubConnection();
            var wrapper = new SignalRHubConnectionWrapper(connection);

            // Act & Assert
            var exception = await Record.ExceptionAsync(async () =>
            {
                await wrapper.DisposeAsync();
            });
            
            Assert.Null(exception);
        }
    }
}
