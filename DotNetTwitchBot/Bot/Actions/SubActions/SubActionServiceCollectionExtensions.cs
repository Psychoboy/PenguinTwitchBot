using Microsoft.Extensions.DependencyInjection;

namespace DotNetTwitchBot.Bot.Actions.SubActions
{
    /// <summary>
    /// Extension methods for automatically registering SubAction handlers in DI.
    /// This eliminates the need to manually register each handler in BotCommandsRegistry.
    /// </summary>
    public static class SubActionServiceCollectionExtensions
    {
        /// <summary>
        /// Automatically discovers and registers all SubAction handlers.
        /// Call this from BotCommandsRegistry.AddBotCommands().
        /// </summary>
        public static IServiceCollection AddSubActionHandlers(this IServiceCollection services)
        {
            // Get all handler types from the assembly
            var assembly = typeof(ISubActionHandler).Assembly;
            var handlerTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(ISubActionHandler).IsAssignableFrom(t))
                .ToList();

            // Register each handler as transient by both interface and concrete type
            // This allows resolving by concrete type when creating new scopes for concurrency safety
            foreach (var handlerType in handlerTypes)
            {
                services.AddTransient(typeof(ISubActionHandler), handlerType);
                services.AddTransient(handlerType); // Register by concrete type too
            }

            // Register the factory
            services.AddTransient<SubActionHandlerFactory>();

            // Register HttpClient for handlers that need it (e.g., ExternalApiHandler)
            services.AddHttpClient();

            return services;
        }
    }
}
