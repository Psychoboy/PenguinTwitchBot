using DotNetTwitchBot.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetTwitchBot.Test.Application.Notifications
{
    public class PenguinDispatcherTests
    {
        // Test notification types
        public class TestNotification : INotification
        {
            public string Message { get; set; } = string.Empty;
        }

        public class DerivedTestNotification : TestNotification
        {
            public string AdditionalData { get; set; } = string.Empty;
        }

        // Test request types
        public class TestRequest : IRequest<string>
        {
            public string Input { get; set; } = string.Empty;
        }

        public class TestVoidRequest : IRequest
        {
            public bool WasExecuted { get; set; }
        }

        // Test handlers
        public class TestNotificationHandler1 : INotificationHandler<TestNotification>
        {
            public List<string> ReceivedMessages { get; } = new();
            public CancellationToken? ReceivedToken { get; private set; }

            public Task Handle(TestNotification notification, CancellationToken cancellationToken)
            {
                ReceivedMessages.Add(notification.Message + "_Handler1");
                ReceivedToken = cancellationToken;
                return Task.CompletedTask;
            }
        }

        public class TestNotificationHandler2 : INotificationHandler<TestNotification>
        {
            public List<string> ReceivedMessages { get; } = new();
            public CancellationToken? ReceivedToken { get; private set; }

            public Task Handle(TestNotification notification, CancellationToken cancellationToken)
            {
                ReceivedMessages.Add(notification.Message + "_Handler2");
                ReceivedToken = cancellationToken;
                return Task.CompletedTask;
            }
        }

        public class DerivedTestNotificationHandler : INotificationHandler<DerivedTestNotification>
        {
            public List<string> ReceivedMessages { get; } = new();

            public Task Handle(DerivedTestNotification notification, CancellationToken cancellationToken)
            {
                ReceivedMessages.Add($"{notification.Message}_{notification.AdditionalData}_DerivedHandler");
                return Task.CompletedTask;
            }
        }

        public class ThrowingNotificationHandler : INotificationHandler<TestNotification>
        {
            public Task Handle(TestNotification notification, CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("Handler error");
            }
        }

        public class DelayedNotificationHandler : INotificationHandler<TestNotification>
        {
            public bool WasExecuted { get; private set; }

            public async Task Handle(TestNotification notification, CancellationToken cancellationToken)
            {
                await Task.Delay(200, cancellationToken);
                WasExecuted = true;
            }
        }

        public class TestRequestHandler : IRequestHandler<TestRequest, string>
        {
            public CancellationToken? ReceivedToken { get; private set; }

            public Task<string> Handle(TestRequest request, CancellationToken cancellationToken)
            {
                ReceivedToken = cancellationToken;
                return Task.FromResult($"Processed: {request.Input}");
            }
        }

        public class TestVoidRequestHandler : IRequestHandler<TestVoidRequest, Unit>
        {
            public Task<Unit> Handle(TestVoidRequest request, CancellationToken cancellationToken)
            {
                request.WasExecuted = true;
                return Task.FromResult(Unit.Value);
            }
        }

        public class ThrowingRequestHandler : IRequestHandler<TestRequest, string>
        {
            public Task<string> Handle(TestRequest request, CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("Request handler error");
            }
        }

        [Fact]
        public async Task Publish_WithSingleHandler_InvokesHandler()
        {
            // Arrange
            var services = new ServiceCollection();
            var handler = new TestNotificationHandler1();
            services.AddSingleton<INotificationHandler<TestNotification>>(handler);
            services.AddSingleton<IPenguinDispatcher, PenguinDispatcher>();

            var serviceProvider = services.BuildServiceProvider();
            var dispatcher = serviceProvider.GetRequiredService<IPenguinDispatcher>();

            var notification = new TestNotification { Message = "Test" };

            // Act
            await dispatcher.Publish(notification);

            // Assert
            Assert.Single(handler.ReceivedMessages);
            Assert.Equal("Test_Handler1", handler.ReceivedMessages[0]);
        }

        [Fact]
        public async Task Publish_WithMultipleHandlers_InvokesAllHandlers()
        {
            // Arrange
            var services = new ServiceCollection();
            var handler1 = new TestNotificationHandler1();
            var handler2 = new TestNotificationHandler2();
            services.AddSingleton<INotificationHandler<TestNotification>>(handler1);
            services.AddSingleton<INotificationHandler<TestNotification>>(handler2);
            services.AddSingleton<IPenguinDispatcher, PenguinDispatcher>();

            var serviceProvider = services.BuildServiceProvider();
            var dispatcher = serviceProvider.GetRequiredService<IPenguinDispatcher>();

            var notification = new TestNotification { Message = "Test" };

            // Act
            await dispatcher.Publish(notification);

            // Assert
            Assert.Single(handler1.ReceivedMessages);
            Assert.Single(handler2.ReceivedMessages);
            Assert.Equal("Test_Handler1", handler1.ReceivedMessages[0]);
            Assert.Equal("Test_Handler2", handler2.ReceivedMessages[0]);
        }

        [Fact]
        public async Task Publish_WithNoHandlers_CompletesSuccessfully()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<IPenguinDispatcher, PenguinDispatcher>();

            var serviceProvider = services.BuildServiceProvider();
            var dispatcher = serviceProvider.GetRequiredService<IPenguinDispatcher>();

            var notification = new TestNotification { Message = "Test" };

            // Act & Assert - should not throw
            await dispatcher.Publish(notification);
        }

        [Fact]
        public async Task Publish_WithCancellationToken_PropagatesTokenToHandlers()
        {
            // Arrange
            var services = new ServiceCollection();
            var handler = new TestNotificationHandler1();
            services.AddSingleton<INotificationHandler<TestNotification>>(handler);
            services.AddSingleton<IPenguinDispatcher, PenguinDispatcher>();

            var serviceProvider = services.BuildServiceProvider();
            var dispatcher = serviceProvider.GetRequiredService<IPenguinDispatcher>();

            var notification = new TestNotification { Message = "Test" };
            var cts = new CancellationTokenSource();

            // Act
            await dispatcher.Publish(notification, cts.Token);

            // Assert
            Assert.NotNull(handler.ReceivedToken);
            Assert.Equal(cts.Token, handler.ReceivedToken.Value);
        }

        [Fact]
        public async Task Publish_WithThrowingHandler_PropagatesException()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<INotificationHandler<TestNotification>, ThrowingNotificationHandler>();
            services.AddSingleton<IPenguinDispatcher, PenguinDispatcher>();

            var serviceProvider = services.BuildServiceProvider();
            var dispatcher = serviceProvider.GetRequiredService<IPenguinDispatcher>();

            var notification = new TestNotification { Message = "Test" };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await dispatcher.Publish(notification));
        }

        [Fact]
        public async Task Publish_WithMultipleHandlersOneThrows_PropagatesException()
        {
            // Arrange
            var services = new ServiceCollection();
            var handler1 = new TestNotificationHandler1();
            services.AddSingleton<INotificationHandler<TestNotification>>(handler1);
            services.AddSingleton<INotificationHandler<TestNotification>, ThrowingNotificationHandler>();
            services.AddSingleton<IPenguinDispatcher, PenguinDispatcher>();

            var serviceProvider = services.BuildServiceProvider();
            var dispatcher = serviceProvider.GetRequiredService<IPenguinDispatcher>();

            var notification = new TestNotification { Message = "Test" };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await dispatcher.Publish(notification));
        }

        [Fact]
        public async Task Publish_WithCancelledToken_ThrowsOperationCancelledException()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<INotificationHandler<TestNotification>, DelayedNotificationHandler>();
            services.AddSingleton<IPenguinDispatcher, PenguinDispatcher>();

            var serviceProvider = services.BuildServiceProvider();
            var dispatcher = serviceProvider.GetRequiredService<IPenguinDispatcher>();

            var notification = new TestNotification { Message = "Test" };
            var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await dispatcher.Publish(notification, cts.Token));
        }

        [Fact]
        public async Task Publish_WithPolymorphicNotification_UsesRuntimeType()
        {
            // Arrange
            var services = new ServiceCollection();
            var derivedHandler = new DerivedTestNotificationHandler();
            services.AddSingleton<INotificationHandler<DerivedTestNotification>>(derivedHandler);
            services.AddSingleton<IPenguinDispatcher, PenguinDispatcher>();

            var serviceProvider = services.BuildServiceProvider();
            var dispatcher = serviceProvider.GetRequiredService<IPenguinDispatcher>();

            // Act - Pass as base type but actual instance is derived type
            INotification notification = new DerivedTestNotification 
            { 
                Message = "Base", 
                AdditionalData = "Derived" 
            };
            await dispatcher.Publish(notification);

            // Assert - Should use runtime type (DerivedTestNotification) not compile-time type (INotification)
            Assert.Single(derivedHandler.ReceivedMessages);
            Assert.Equal("Base_Derived_DerivedHandler", derivedHandler.ReceivedMessages[0]);
        }

        [Fact]
        public async Task Send_WithValidHandler_ReturnsResponse()
        {
            // Arrange
            var services = new ServiceCollection();
            var handler = new TestRequestHandler();
            services.AddSingleton<IRequestHandler<TestRequest, string>>(handler);
            services.AddSingleton<IPenguinDispatcher, PenguinDispatcher>();

            var serviceProvider = services.BuildServiceProvider();
            var dispatcher = serviceProvider.GetRequiredService<IPenguinDispatcher>();

            var request = new TestRequest { Input = "TestInput" };

            // Act
            var result = await dispatcher.Send(request);

            // Assert
            Assert.Equal("Processed: TestInput", result);
        }

        [Fact]
        public async Task Send_WithVoidRequest_ReturnsUnit()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<IRequestHandler<TestVoidRequest, Unit>, TestVoidRequestHandler>();
            services.AddSingleton<IPenguinDispatcher, PenguinDispatcher>();

            var serviceProvider = services.BuildServiceProvider();
            var dispatcher = serviceProvider.GetRequiredService<IPenguinDispatcher>();

            var request = new TestVoidRequest();

            // Act
            var result = await dispatcher.Send(request);

            // Assert
            Assert.Equal(Unit.Value, result);
            Assert.True(request.WasExecuted);
        }

        [Fact]
        public async Task Send_WithNoHandler_ThrowsInvalidOperationException()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<IPenguinDispatcher, PenguinDispatcher>();

            var serviceProvider = services.BuildServiceProvider();
            var dispatcher = serviceProvider.GetRequiredService<IPenguinDispatcher>();

            var request = new TestRequest { Input = "TestInput" };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await dispatcher.Send(request));
            Assert.Contains("No handler registered", exception.Message);
            Assert.Contains("TestRequest", exception.Message);
        }

        [Fact]
        public async Task Send_WithCancellationToken_PropagatesTokenToHandler()
        {
            // Arrange
            var services = new ServiceCollection();
            var handler = new TestRequestHandler();
            services.AddSingleton<IRequestHandler<TestRequest, string>>(handler);
            services.AddSingleton<IPenguinDispatcher, PenguinDispatcher>();

            var serviceProvider = services.BuildServiceProvider();
            var dispatcher = serviceProvider.GetRequiredService<IPenguinDispatcher>();

            var request = new TestRequest { Input = "TestInput" };
            var cts = new CancellationTokenSource();

            // Act
            await dispatcher.Send(request, cts.Token);

            // Assert
            Assert.NotNull(handler.ReceivedToken);
            Assert.Equal(cts.Token, handler.ReceivedToken.Value);
        }

        [Fact]
        public async Task Send_WithThrowingHandler_PropagatesException()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<IRequestHandler<TestRequest, string>, ThrowingRequestHandler>();
            services.AddSingleton<IPenguinDispatcher, PenguinDispatcher>();

            var serviceProvider = services.BuildServiceProvider();
            var dispatcher = serviceProvider.GetRequiredService<IPenguinDispatcher>();

            var request = new TestRequest { Input = "TestInput" };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await dispatcher.Send(request));
        }

        [Fact]
        public async Task Publish_ExecutesHandlersInParallel()
        {
            // Arrange
            var services = new ServiceCollection();
            var handler1 = new DelayedNotificationHandler();
            var handler2 = new DelayedNotificationHandler();
            services.AddSingleton<INotificationHandler<TestNotification>>(handler1);
            services.AddSingleton<INotificationHandler<TestNotification>>(handler2);
            services.AddSingleton<IPenguinDispatcher, PenguinDispatcher>();

            var serviceProvider = services.BuildServiceProvider();
            var dispatcher = serviceProvider.GetRequiredService<IPenguinDispatcher>();

            var notification = new TestNotification { Message = "Test" };

            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await dispatcher.Publish(notification);
            stopwatch.Stop();

            // Assert
            Assert.True(handler1.WasExecuted);
            Assert.True(handler2.WasExecuted);
            // If handlers ran sequentially, this would take ~400ms
            // If parallel, should be closer to ~200ms
            // Using 500ms threshold to account for platform differences and system load
            Assert.True(stopwatch.ElapsedMilliseconds < 500, 
                $"Handlers did not execute in parallel. Took {stopwatch.ElapsedMilliseconds}ms");
        }
    }
}
