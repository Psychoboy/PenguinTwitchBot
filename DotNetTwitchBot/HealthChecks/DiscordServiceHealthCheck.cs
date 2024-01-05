using DotNetTwitchBot.Bot.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetTwitchBot.HealthChecks
{
    public class DiscordServiceHealthCheck : IHealthCheck
    {
        private readonly IDiscordService _discordService;

        public DiscordServiceHealthCheck(IDiscordService discordService)
        {
            _discordService = discordService;
        }
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var connectionState = _discordService.ServiceStatus();
                switch (connectionState)
                {
                    case Discord.ConnectionState.Connected:
                        return Task.FromResult(HealthCheckResult.Healthy());
                    case Discord.ConnectionState.Disconnected:
                        return Task.FromResult(HealthCheckResult.Unhealthy("Discord bot is disconnected."));
                    case Discord.ConnectionState.Disconnecting:
                    case Discord.ConnectionState.Connecting:
                        return Task.FromResult(HealthCheckResult.Degraded("Discord bot is connecting or disconnecting."));
                }
                return Task.FromResult(HealthCheckResult.Unhealthy());

            }
            catch
            {
                return Task.FromResult(HealthCheckResult.Unhealthy());
            }

        }
    }
}
