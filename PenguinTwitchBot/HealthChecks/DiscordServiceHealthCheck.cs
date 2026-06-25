using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Database.Bot.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PenguinTwitchBot.HealthChecks
{
    public class DiscordServiceHealthCheck : IHealthCheck
    {
        private readonly IDiscordService _discordService;
        private readonly DiscordSettings _discordSettings;

        public DiscordServiceHealthCheck(IDiscordService discordService, IConfiguration configuration)
        {
            _discordService = discordService;
            _discordSettings = configuration.GetRequiredSection("Discord").Get<DiscordSettings>() ?? new DiscordSettings();
        }
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_discordSettings.DiscordToken))
                return Task.FromResult(HealthCheckResult.Healthy("Discord is not configured."));

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
