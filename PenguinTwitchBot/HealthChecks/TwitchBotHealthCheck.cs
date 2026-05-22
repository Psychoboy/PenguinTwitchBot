using PenguinTwitchBot.Bot.TwitchServices;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PenguinTwitchBot.HealthChecks
{
    public class TwitchBotHealthCheck(ITwitchChatBot twitchChatBot) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var isConnected = await twitchChatBot.IsConnected();
            if (isConnected)
            {
                return HealthCheckResult.Healthy();
            }
            else
            {
                return HealthCheckResult.Unhealthy();
            }
        }
    }
}
