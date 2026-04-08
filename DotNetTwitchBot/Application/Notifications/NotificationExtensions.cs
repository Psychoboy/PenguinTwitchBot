using System.Reflection;

namespace DotNetTwitchBot.Application.Notifications
{
    public static class NotificationExtensions
    {
        public static IServiceCollection AddPenguinDispatcher(this IServiceCollection services, Assembly assembly)
        {
            services.AddSingleton<IPenguinDispatcher, PenguinDispatcher>();

            var types = assembly.GetTypes();

            // Register notification handlers
            foreach (var type in types.Where(t => t.IsClass && !t.IsAbstract))
            {
                var interfaces = type.GetInterfaces();
                foreach (var @interface in interfaces)
                {
                    if (@interface.IsGenericType)
                    {
                        var genericTypeDef = @interface.GetGenericTypeDefinition();
                        if (genericTypeDef == typeof(INotificationHandler<>))
                        {
                            services.AddTransient(@interface, type);
                        }
                        else if (genericTypeDef == typeof(IRequestHandler<,>))
                        {
                            services.AddTransient(@interface, type);
                        }
                    }
                }
            }

            return services;
        }
    }
}
