using PenguinTwitchBot.Application.Notifications;
using Xunit;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace PenguinTwitchBot.Test.Application.Notifications
{
    public class NotificationInterfacesTests
    {
        [Fact]
        public void INotification_IsEmptyInterface()
        {
            var type = typeof(INotification);
            Assert.True(type.IsInterface);
        }

        [Fact]
        public void INotificationHandler_IsGenericInterface()
        {
            var type = typeof(INotificationHandler<>);
            Assert.True(type.IsInterface);
            Assert.True(type.IsGenericType);
        }

        [Fact]
        public void INotificationHandler_HasHandleMethod()
        {
            var method = typeof(INotificationHandler<>).GetMethod("Handle");
            Assert.NotNull(method);
        }

        [Fact]
        public void IPenguinDispatcher_IsInterface()
        {
            Assert.True(typeof(IPenguinDispatcher).IsInterface);
        }

        [Fact]
        public void IRequest_IsGenericInterface()
        {
            var type = typeof(IRequest<>);
            Assert.True(type.IsInterface);
            Assert.True(type.IsGenericType);
        }
    }

    public class NotificationExtensionsTests
    {
        [Fact]
        public void AddPenguinDispatcher_RegistersDispatcher()
        {
            var services = new ServiceCollection();
            var assembly = typeof(NotificationExtensions).Assembly;
            
            services.AddPenguinDispatcher(assembly);

            var provider = services.BuildServiceProvider();
            var dispatcher = provider.GetService<IPenguinDispatcher>();
            Assert.NotNull(dispatcher);
        }

        [Fact]
        public void AddPenguinDispatcher_ReturnsServiceCollection()
        {
            var services = new ServiceCollection();
            var assembly = typeof(NotificationExtensions).Assembly;
            
            var result = services.AddPenguinDispatcher(assembly);

            Assert.Same(services, result);
        }
    }
}