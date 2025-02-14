using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.TwitchServices;

namespace DotNetTwitchBot.Bot.ServiceTools
{
    public class ServiceMaintenance(IServiceScopeFactory scopeFactory) : IServiceMaintenance
    {
        public IEnumerable<Type> GetServiceTypes()
        {
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => typeof(IHostedService).IsAssignableFrom(p) && p.IsClass);
            List<Type> serviceTypes = new();
            foreach (var type in types)
            {
                var interfaces = type.GetInterfaces();
                foreach (var i in interfaces)
                {
                    if (i == typeof(IHostedService) || i ==typeof(IBaseCommandService) ||
                        i == typeof(IDisposable))
                    {
                        continue;
                    }
                    serviceTypes.Add(i);
                }
            }
            serviceTypes.Sort((a, b) => string.Compare(a.Name, b.Name));
            return serviceTypes;
        }
        public async Task RestartService(Type serviceType)
        {
            var source = new CancellationTokenSource();
            var token = source.Token;
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService(serviceType);
            if (serviceType == typeof(ITwitchWebsocketHostedService))
            {
                var wsService = (ITwitchWebsocketHostedService)service;
                await wsService.ForceReconnect();
                return;
            }
            var method = service.GetType().GetMethod("StopAsync");
            if (method != null)
            {
                await (Task)method.Invoke(service, new object[] { token });
            }
            if(serviceType == typeof(IDiscordService))
            {
                // Time to disconnect from discord
                await Task.Delay(5000);
            }
            method = service.GetType().GetMethod("StartAsync");
            if (method != null)
            {
                await (Task)method.Invoke(service, new object[] { token });
            }
        }
    }
}