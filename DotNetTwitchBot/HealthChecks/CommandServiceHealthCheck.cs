using DotNetTwitchBot.Bot.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetTwitchBot.HealthChecks
{
    public class CommandServiceHealthCheck : IHealthCheck
    {
        private readonly IServiceBackbone _serviceBackbone;

        public CommandServiceHealthCheck(IServiceBackbone serviceBackbone)
        {
            _serviceBackbone = serviceBackbone;
        }
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var healthy = _serviceBackbone.HealthStatus;
            if (healthy)
            {
                return Task.FromResult(HealthCheckResult.Healthy());
            }
            else
            {
                return Task.FromResult(HealthCheckResult.Degraded());
            }
        }
    }
}
