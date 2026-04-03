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

            // Register each handler as transient
            foreach (var handlerType in handlerTypes)
            {
                services.AddTransient(typeof(ISubActionHandler), handlerType);
            }

            // Register the factory
            services.AddTransient<SubActionHandlerFactory>();

            return services;
        }
    }
}
