using DotNetTwitchBot.Bot.TwitchServices;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetTwitchBot.HealthChecks
{
    public class TwitchBotHealthCheck(TwitchChatBot twitchChatBot) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var isConnected = twitchChatBot.IsConnected();
            var isJoined = twitchChatBot.IsInChannel();
            if (isConnected && isJoined)
            {
                return Task.FromResult(HealthCheckResult.Healthy());
            }
            else if (!isConnected && !isJoined)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy());
            }
            else if (!isConnected && isJoined)
            {
                return Task.FromResult(HealthCheckResult.Degraded("Bot is not connected"));
            }
            else
            {
                return Task.FromResult(HealthCheckResult.Degraded("Bot is not joined to channel"));
            }
        }
    }
}
