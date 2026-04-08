using DotNetTwitchBot.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace DotNetTwitchBot.Test.Application.Notifications
{
    public class NotificationExtensionsTests
    {
        // Test notification and request types for auto-discovery
        private record AutoDiscoveredNotification(string Message) : INotification;
        private record AutoDiscoveredRequest(string Input) : IRequest<string>;
        private record AutoDiscoveredRequestNoResponse : IRequest;

        // Test handlers that should be auto-discovered
        private class AutoDiscoveredNotificationHandler : INotificationHandler<AutoDiscoveredNotification>
        {
            public Task Handle(AutoDiscoveredNotification notification, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        private class AutoDiscoveredRequestHandler : IRequestHandler<AutoDiscoveredRequest, string>
        {
            public Task<string> Handle(AutoDiscoveredRequest request, CancellationToken cancellationToken)
            {
                return Task.FromResult("Response");
            }
        }

        private class AutoDiscoveredRequestNoResponseHandler : IRequestHandler<AutoDiscoveredRequestNoResponse>
        {
            public Task Handle(AutoDiscoveredRequestNoResponse request, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        // Handler with multiple notification types
        private record FirstNotification : INotification;
        private record SecondNotification : INotification;

        private class MultiNotificationHandler : 
            INotificationHandler<FirstNotification>,
            INotificationHandler<SecondNotification>
        {
            public Task Handle(FirstNotification notification, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task Handle(SecondNotification notification, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        [Fact]
        public void AddNotifications_RegistersPublisher()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddNotifications(Assembly.GetExecutingAssembly());
            var provider = services.BuildServiceProvider();

            // Assert
            var publisher = provider.GetService<INotificationPublisher>();
            Assert.NotNull(publisher);
            Assert.IsType<NotificationPublisher>(publisher);
        }

        [Fact]
        public void AddNotifications_RegistersMediator()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddNotifications(Assembly.GetExecutingAssembly());
            var provider = services.BuildServiceProvider();

            // Assert
            var mediator = provider.GetService<IPenguinDispatcher>();
            Assert.NotNull(mediator);
            Assert.IsType<NotificationPublisher>(mediator);
        }

        [Fact]
        public void AddNotifications_PublisherAndMediatorAreSameInstance()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddNotifications(Assembly.GetExecutingAssembly());
            var provider = services.BuildServiceProvider();

            // Assert
            var publisher = provider.GetRequiredService<INotificationPublisher>();
            var mediator = provider.GetRequiredService<IPenguinDispatcher>();
            Assert.Same(publisher, mediator);
        }

        [Fact]
        public void AddNotifications_AutoDiscoversNotificationHandlers()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddNotifications(Assembly.GetExecutingAssembly());
            var provider = services.BuildServiceProvider();

            // Assert
            var handlers = provider.GetServices<INotificationHandler<AutoDiscoveredNotification>>();
            Assert.Single(handlers);
            Assert.IsType<AutoDiscoveredNotificationHandler>(handlers.First());
        }

        [Fact]
        public void AddNotifications_AutoDiscoversRequestHandlers()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddNotifications(Assembly.GetExecutingAssembly());
            var provider = services.BuildServiceProvider();

            // Assert
            var handler = provider.GetService<IRequestHandler<AutoDiscoveredRequest, string>>();
            Assert.NotNull(handler);
            Assert.IsType<AutoDiscoveredRequestHandler>(handler);
        }

        [Fact]
        public void AddNotifications_AutoDiscoversRequestHandlersWithoutResponse()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddNotifications(Assembly.GetExecutingAssembly());
            var provider = services.BuildServiceProvider();

            // Assert
            var handler = provider.GetService<IRequestHandler<AutoDiscoveredRequestNoResponse>>();
            Assert.NotNull(handler);
            Assert.IsType<AutoDiscoveredRequestNoResponseHandler>(handler);
        }

        [Fact]
        public void AddNotifications_HandlersAreTransient()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddNotifications(Assembly.GetExecutingAssembly());
            var provider = services.BuildServiceProvider();

            // Assert
            var handler1 = provider.GetService<INotificationHandler<AutoDiscoveredNotification>>();
            var handler2 = provider.GetService<INotificationHandler<AutoDiscoveredNotification>>();
            Assert.NotSame(handler1, handler2);
        }

        [Fact]
        public void AddNotifications_PublisherIsSingleton()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddNotifications(Assembly.GetExecutingAssembly());
            var provider = services.BuildServiceProvider();

            // Assert
            var publisher1 = provider.GetRequiredService<INotificationPublisher>();
            var publisher2 = provider.GetRequiredService<INotificationPublisher>();
            Assert.Same(publisher1, publisher2);
        }

        [Fact]
        public void AddNotifications_RegistersHandlerForMultipleNotificationTypes()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddNotifications(Assembly.GetExecutingAssembly());
            var provider = services.BuildServiceProvider();

            // Assert
            var firstHandlers = provider.GetServices<INotificationHandler<FirstNotification>>();
            var secondHandlers = provider.GetServices<INotificationHandler<SecondNotification>>();
            
            Assert.Single(firstHandlers);
            Assert.Single(secondHandlers);
            Assert.IsType<MultiNotificationHandler>(firstHandlers.First());
            Assert.IsType<MultiNotificationHandler>(secondHandlers.First());
        }

        [Fact]
        public async Task AddNotifications_IntegrationTest_NotificationsWork()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddNotifications(Assembly.GetExecutingAssembly());
            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<INotificationPublisher>();

            // Act & Assert - should not throw
            await publisher.Publish(new AutoDiscoveredNotification("Test"));
        }

        [Fact]
        public async Task AddNotifications_IntegrationTest_RequestsWork()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddNotifications(Assembly.GetExecutingAssembly());
            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IPenguinDispatcher>();

            // Act
            var result = await mediator.Send(new AutoDiscoveredRequest("Input"));

            // Assert
            Assert.Equal("Response", result);
        }

        [Fact]
        public void AddNotifications_WithEmptyAssembly_DoesNotThrow()
        {
            // Arrange
            var services = new ServiceCollection();
            var emptyAssembly = typeof(object).Assembly; // System assembly with no handlers

            // Act & Assert
            services.AddNotifications(emptyAssembly);
            var provider = services.BuildServiceProvider();
            var publisher = provider.GetService<INotificationPublisher>();
            Assert.NotNull(publisher);
        }
    }
}
