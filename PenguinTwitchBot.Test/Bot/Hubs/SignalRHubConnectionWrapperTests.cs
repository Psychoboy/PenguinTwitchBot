using Xunit;
using Moq;
using Moq.Protected;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using PenguinTwitchBot.Bot.Hubs;

namespace PenguinTwitchBot.Test.Bot.Hubs
{
    public class SignalRHubConnectionWrapperTests
    {
        private (HubConnection Connection, Mock<HttpMessageHandler> Handler) CreateConfigurableHubConnection()
        {
            var mockHandler = new Mock<HttpMessageHandler>(MockBehavior.Loose);
            var capturedHandler = mockHandler.Object;

            var connection = new HubConnectionBuilder()
                .WithUrl("http://localhost/dummyHub", options =>
                {
                    options.HttpMessageHandlerFactory = _ => capturedHandler;
                })
                .AddJsonProtocol() 
                .Build();

            return (connection, mockHandler);
        }

        [Fact]
        public async Task StartAsync_ShouldReturnEarly_IfAlreadyStarted()
        {
            // Arrange
            var (connection, mockHandler) = CreateConfigurableHubConnection();
            var wrapper = new SignalRHubConnectionWrapper(connection);

            // Force private '_started' field to true to target the early exit condition
            var startedField = typeof(SignalRHubConnectionWrapper)
                .GetField("_started", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            startedField.SetValue(wrapper, true);

            // Act
            var exception = await Record.ExceptionAsync(async () => await wrapper.StartAsync());

            // Assert - Confirms it exited instantly with 0 network calls
            Assert.Null(exception);

            mockHandler.Protected().Verify(
                "SendAsync", 
                Times.Never(),
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("negotiate")), 
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Fact]
        public async Task StartAsync_ShouldRetryAndRethrowFinalException_WhenMaxRetriesExceeded()
        {
            // Arrange
            var (connection, mockHandler) = CreateConfigurableHubConnection();
            
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.InternalServerError)); // Force network drops

            var wrapper = new SignalRHubConnectionWrapper(connection);

            // Act & Assert - Verifies that the loop exhausts all 5 attempts and bubbles up the exception
            await Assert.ThrowsAsync<HttpRequestException>(async () => await wrapper.StartAsync());
            
            mockHandler.Protected().Verify(
                "SendAsync", 
                Times.Exactly(5), 
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("negotiate")), 
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Fact]
        public void Constructor_WithNullConnection_ShouldThrowArgumentNullException()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new SignalRHubConnectionWrapper(null!));
            Assert.Equal("connection", exception.ParamName);
        }

        [Fact]
        public void On_ShouldInvokeUnderlyingOnMethodAndReturnDisposable()
        {
            // Arrange
            var (connection, _) = CreateConfigurableHubConnection();
            var wrapper = new SignalRHubConnectionWrapper(connection);
            Func<string, Task> handler = (msg) => Task.CompletedTask;

            // Act
            var disposable = wrapper.On<string>("TestEvent", handler);

            // Assert
            Assert.NotNull(disposable);
            disposable.Dispose();
        }

        [Fact]
        public async Task DisposeAsync_ShouldInvokeUnderlyingDispose()
        {
            // Arrange
            var (connection, _) = CreateConfigurableHubConnection();
            var wrapper = new SignalRHubConnectionWrapper(connection);

            // Act
            var exception = await Record.ExceptionAsync(async () => await wrapper.DisposeAsync());

            // Assert
            Assert.Null(exception);
        }
    }
}
