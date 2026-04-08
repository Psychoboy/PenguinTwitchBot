using DotNetTwitchBot.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetTwitchBot.Test.Application.Notifications
{
    public class NotificationPublisherTests
    {
        // Test notification types
        private record TestNotification(string Message) : INotification;
        private record AnotherNotification(int Value) : INotification;

        // Test request types
        private record TestRequest(string Input) : IRequest<string>;
        private record TestRequestNoResponse(string Input) : IRequest;
        private record FailingRequest : IRequest<string>;

        // Test handlers
        private class TestNotificationHandler : INotificationHandler<TestNotification>
        {
            public List<string> ReceivedMessages { get; } = new();

            public Task Handle(TestNotification notification, CancellationToken cancellationToken)
            {
                ReceivedMessages.Add(notification.Message);
                return Task.CompletedTask;
            }
        }

        private class SecondTestNotificationHandler : INotificationHandler<TestNotification>
        {
            public List<string> ReceivedMessages { get; } = new();

            public Task Handle(TestNotification notification, CancellationToken cancellationToken)
            {
                ReceivedMessages.Add($"Second: {notification.Message}");
                return Task.CompletedTask;
            }
        }

        private class AnotherNotificationHandler : INotificationHandler<AnotherNotification>
        {
            public int ReceivedValue { get; private set; }

            public Task Handle(AnotherNotification notification, CancellationToken cancellationToken)
            {
                ReceivedValue = notification.Value;
                return Task.CompletedTask;
            }
        }

        private class TestRequestHandler : IRequestHandler<TestRequest, string>
        {
            public Task<string> Handle(TestRequest request, CancellationToken cancellationToken)
            {
                return Task.FromResult($"Processed: {request.Input}");
            }
        }

        private class TestRequestNoResponseHandler : IRequestHandler<TestRequestNoResponse>
        {
            public bool WasCalled { get; private set; }

            public Task Handle(TestRequestNoResponse request, CancellationToken cancellationToken)
            {
                WasCalled = true;
                return Task.CompletedTask;
            }
        }

        private class FailingRequestHandler : IRequestHandler<FailingRequest, string>
        {
            public Task<string> Handle(FailingRequest request, CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("Handler failed");
            }
        }

        [Fact]
        public async Task Publish_SingleHandler_InvokesHandler()
        {
            // Arrange
            var services = new ServiceCollection();
            var handler = new TestNotificationHandler();
            services.AddSingleton<INotificationHandler<TestNotification>>(handler);
            services.AddSingleton<INotificationPublisher, NotificationPublisher>();
            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<INotificationPublisher>();

            // Act
            await publisher.Publish(new TestNotification("Hello"));

            // Assert
            Assert.Single(handler.ReceivedMessages);
            Assert.Equal("Hello", handler.ReceivedMessages[0]);
        }

        [Fact]
        public async Task Publish_MultipleHandlers_InvokesAllHandlers()
        {
            // Arrange
            var services = new ServiceCollection();
            var handler1 = new TestNotificationHandler();
            var handler2 = new SecondTestNotificationHandler();
            services.AddSingleton<INotificationHandler<TestNotification>>(handler1);
            services.AddSingleton<INotificationHandler<TestNotification>>(handler2);
            services.AddSingleton<INotificationPublisher, NotificationPublisher>();
            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<INotificationPublisher>();

            // Act
            await publisher.Publish(new TestNotification("Test"));

            // Assert
            Assert.Single(handler1.ReceivedMessages);
            Assert.Equal("Test", handler1.ReceivedMessages[0]);
            Assert.Single(handler2.ReceivedMessages);
            Assert.Equal("Second: Test", handler2.ReceivedMessages[0]);
        }

        [Fact]
        public async Task Publish_NoHandlers_DoesNotThrow()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<INotificationPublisher, NotificationPublisher>();
            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<INotificationPublisher>();

            // Act & Assert - should not throw
            await publisher.Publish(new TestNotification("NoHandler"));
        }

        [Fact]
        public async Task Publish_DifferentNotificationTypes_InvokesCorrectHandlers()
        {
            // Arrange
            var services = new ServiceCollection();
            var testHandler = new TestNotificationHandler();
            var anotherHandler = new AnotherNotificationHandler();
            services.AddSingleton<INotificationHandler<TestNotification>>(testHandler);
            services.AddSingleton<INotificationHandler<AnotherNotification>>(anotherHandler);
            services.AddSingleton<INotificationPublisher, NotificationPublisher>();
            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<INotificationPublisher>();

            // Act
            await publisher.Publish(new TestNotification("Message"));
            await publisher.Publish(new AnotherNotification(42));

            // Assert
            Assert.Single(testHandler.ReceivedMessages);
            Assert.Equal("Message", testHandler.ReceivedMessages[0]);
            Assert.Equal(42, anotherHandler.ReceivedValue);
        }

        [Fact]
        public async Task Send_WithResponse_ReturnsCorrectValue()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<IRequestHandler<TestRequest, string>, TestRequestHandler>();
            services.AddSingleton<IPenguinDispatcher, NotificationPublisher>();
            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IPenguinDispatcher>();

            // Act
            var result = await mediator.Send(new TestRequest("Input"));

            // Assert
            Assert.Equal("Processed: Input", result);
        }

        [Fact]
        public async Task Send_WithoutResponse_InvokesHandler()
        {
            // Arrange
            var services = new ServiceCollection();
            var handler = new TestRequestNoResponseHandler();
            services.AddSingleton<IRequestHandler<TestRequestNoResponse>>(handler);
            services.AddSingleton<IPenguinDispatcher, NotificationPublisher>();
            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IPenguinDispatcher>();

            // Act
            await mediator.Send(new TestRequestNoResponse("Test"));

            // Assert
            Assert.True(handler.WasCalled);
        }

        [Fact]
        public async Task Send_NoHandler_ThrowsInvalidOperationException()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<IPenguinDispatcher, NotificationPublisher>();
            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IPenguinDispatcher>();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await mediator.Send(new TestRequest("NoHandler")));
        }

        [Fact]
        public async Task Send_HandlerThrowsException_PropagatesException()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<IRequestHandler<FailingRequest, string>, FailingRequestHandler>();
            services.AddSingleton<IPenguinDispatcher, NotificationPublisher>();
            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IPenguinDispatcher>();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await mediator.Send(new FailingRequest()));
            Assert.Equal("Handler failed", exception.Message);
        }

        [Fact]
        public async Task Publish_CancellationToken_PassedToHandler()
        {
            // Arrange
            var services = new ServiceCollection();
            CancellationToken receivedToken = default;
            services.AddSingleton<INotificationHandler<TestNotification>>(
                new TestNotificationHandlerWithCancellation(token => receivedToken = token));
            services.AddSingleton<INotificationPublisher, NotificationPublisher>();
            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<INotificationPublisher>();
            var cts = new CancellationTokenSource();

            // Act
            await publisher.Publish(new TestNotification("Test"), cts.Token);

            // Assert
            Assert.Equal(cts.Token, receivedToken);
        }

        [Fact]
        public async Task MediatorImplementsBothInterfaces()
        {
            // Arrange
            var services = new ServiceCollection();
            var handler = new TestNotificationHandler();
            services.AddSingleton<INotificationHandler<TestNotification>>(handler);
            services.AddSingleton<NotificationPublisher>();
            services.AddSingleton<IPenguinDispatcher>(sp => sp.GetRequiredService<NotificationPublisher>());
            services.AddSingleton<INotificationPublisher>(sp => sp.GetRequiredService<NotificationPublisher>());
            var provider = services.BuildServiceProvider();

            var mediator = provider.GetRequiredService<IPenguinDispatcher>();
            var publisher = provider.GetRequiredService<INotificationPublisher>();

            // Act
            await mediator.Publish(new TestNotification("From Mediator"));
            await publisher.Publish(new TestNotification("From Publisher"));

            // Assert
            Assert.Equal(2, handler.ReceivedMessages.Count);
            Assert.Equal("From Mediator", handler.ReceivedMessages[0]);
            Assert.Equal("From Publisher", handler.ReceivedMessages[1]);
        }

        private class TestNotificationHandlerWithCancellation : INotificationHandler<TestNotification>
        {
            private readonly Action<CancellationToken> _onHandle;

            public TestNotificationHandlerWithCancellation(Action<CancellationToken> onHandle)
            {
                _onHandle = onHandle;
            }

            public Task Handle(TestNotification notification, CancellationToken cancellationToken)
            {
                _onHandle(cancellationToken);
                return Task.CompletedTask;
            }
        }
    }
}
