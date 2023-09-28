using DotNetTwitchBot.Bot.TwitchServices;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetTwitchBot.HealthChecks
{
    public class TwitchBotHealthCheck : IHealthCheck
    {
        private readonly TwitchChatBot _twitchChatBot;

        public TwitchBotHealthCheck(TwitchChatBot twitchChatBot)
        {
            _twitchChatBot = twitchChatBot;
        }
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var isConnected = _twitchChatBot.IsConnected();
            var isJoined = _twitchChatBot.IsInChannel();
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
