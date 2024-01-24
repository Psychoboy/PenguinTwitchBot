using DotNetTwitchBot.Twitch.EventSub.Websockets.Client;
using DotNetTwitchBot.Twitch.EventSub.Websockets.Core.Handler;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DotNetTwitchBot.Twitch.EventSub.Websockets.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="services">ServiceCollection of the DI Container</param>
        /// <param name="scanMarkers">Array of types in which assemblies to search for NotificationHandlers</param>
        /// <returns>the IServiceCollection to enable further fluent additions to it</returns>
        private static IServiceCollection AddNotificationHandlers(this IServiceCollection services, params Type[] scanMarkers)
        {
            foreach (var marker in scanMarkers)
            {
                var types = marker
                    .Assembly.DefinedTypes
                    .Where(x => typeof(INotificationHandler).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)
                    .ToList();

                foreach (var type in types)
                    services.AddSingleton(typeof(INotificationHandler), type);
            }

            return services;
        }

        /// <summary>
        /// Add TwitchLib EventSub Websockets and its needed parts to the DI container
        /// </summary>
        /// <param name="services">ServiceCollection of the DI Container</param>
        /// <returns>the IServiceCollection to enable further fluent additions to it</returns>
        public static IServiceCollection AddTwitchEventSubWebsockets(this IServiceCollection services)
        {
            services.TryAddTransient<WebsocketClient>();
            services.TryAddSingleton(x => new EventSubWebsocketClient(x.GetRequiredService<ILogger<EventSubWebsocketClient>>(), x.GetRequiredService<IEnumerable<INotificationHandler>>(), x.GetRequiredService<IServiceProvider>(), x.GetRequiredService<WebsocketClient>()));
            services.AddNotificationHandlers(typeof(INotificationHandler));
            return services;
        }
    }
}
