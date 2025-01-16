using Discord;
using DotNetTwitchBot.Bot.Core;
using MediatR;

namespace DotNetTwitchBot.Application.Discord
{
    public class DiscordConnectedHandler(IConfiguration configuration, IDiscordService discordService) : INotificationHandler<DiscordConnectedNotification>
    {
        public async Task Handle(DiscordConnectedNotification notification, CancellationToken cancellationToken)
        {
            var settings = configuration.GetRequiredSection("Discord").Get<DiscordSettings>() ?? throw new Exception("Invalid Configuration. Discord settings missing.");
            IGuild guild = notification.Client.GetGuild(settings.DiscordServerId);
            await guild.DownloadUsersAsync(); //Load all users
            var users = await guild.GetUsersAsync();
            foreach (var user in users)
            {
                var activities = user.Activities;
                if (activities.Where(x => x.Type == ActivityType.Streaming && x.Name.Equals("Twitch")).Any())
                {
                    await discordService.UserStreaming(user, true);
                }
                else if (user.RoleIds.Where(x => x == 679556411067465735).Any())
                {
                    await discordService.UserStreaming(user, false);
                }
            }
            await DiscordService.CacheLastMessages(guild);
        }
    }
}
