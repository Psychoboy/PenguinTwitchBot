using System.Reflection;

namespace DotNetTwitchBot.Application.Notifications
{
    /// <summary>
    /// Extension methods for registering notification handlers in DI container
    /// </summary>
    public static class NotificationExtensions
    {
        /// <summary>
        /// Registers the notification publisher and all handlers from the specified assembly
        /// </summary>
        public static IServiceCollection AddNotifications(this IServiceCollection services, Assembly assembly)
        {
            // Register the publisher as both INotificationPublisher and IPenguinDispatcher
            services.AddSingleton<NotificationPublisher>();
            services.AddSingleton<INotificationPublisher>(sp => sp.GetRequiredService<NotificationPublisher>());
            services.AddSingleton<IPenguinDispatcher>(sp => sp.GetRequiredService<NotificationPublisher>());

            // Find all INotificationHandler<T> implementations in the assembly
            var notificationHandlers = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Select(t => new
                {
                    ImplementationType = t,
                    ServiceTypes = t.GetInterfaces()
                        .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INotificationHandler<>))
                        .ToList()
                })
                .Where(x => x.ServiceTypes.Any())
                .ToList();

            // Register each notification handler
            foreach (var handler in notificationHandlers)
            {
                foreach (var serviceType in handler.ServiceTypes)
                {
                    services.AddTransient(serviceType, handler.ImplementationType);
                }
            }

            // Find all IRequestHandler<T> and IRequestHandler<T, TResponse> implementations
            var requestHandlers = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Select(t => new
                {
                    ImplementationType = t,
                    ServiceTypes = t.GetInterfaces()
                        .Where(i => i.IsGenericType && 
                                   (i.GetGenericTypeDefinition() == typeof(IRequestHandler<>) ||
                                    i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
                        .ToList()
                })
                .Where(x => x.ServiceTypes.Any())
                .ToList();

            // Register each request handler
            foreach (var handler in requestHandlers)
            {
                foreach (var serviceType in handler.ServiceTypes)
                {
                    services.AddTransient(serviceType, handler.ImplementationType);
                }
            }

            return services;
        }
    }
}
