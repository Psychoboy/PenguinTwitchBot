using Discord;
using Discord.Net;
using PenguinTwitchBot.Bot.Features;
using PenguinTwitchBot.Bot.Core;

namespace PenguinTwitchBot.Application.Discord
{
    public class DiscordReadyHandler(
        ILogger<DiscordReadyHandler> logger,
        IConfiguration configuration,
        IDiscordService discordService,
        IFeatureRuntimeCoordinator featureRuntimeCoordinator) : Application.Notifications.INotificationHandler<DiscordReadyNotification>
    {
        public async Task Handle(DiscordReadyNotification notification, CancellationToken cancellationToken)
        {
            try
            {
                var settings = configuration.GetSection("Discord").Get<DiscordSettings>();
                if (settings == null || string.IsNullOrWhiteSpace(settings.DiscordToken) || settings.DiscordServerId == 0)
                {
                    logger.LogInformation("Discord is not fully configured. Skipping ready handler initialization.");
                    return;
                }

                IGuild? guild = notification.Client.GetGuild(settings.DiscordServerId);
                if (guild == null)
                {
                    logger.LogInformation("Discord guild {GuildId} is unavailable. Skipping ready handler initialization.", settings.DiscordServerId);
                    return;
                }

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
                {
                    var guildCommand = new SlashCommandBuilder();
                    guildCommand.WithName("gib");
                    guildCommand.WithDescription("Gib Stuff");
                    try
                    {
                        await guild.CreateApplicationCommandAsync(guildCommand.Build());
                    }
                    catch (HttpException exception)
                    {
                        logger.LogError(exception, "Error creating command");
                    }
                }
                {
                    var guildCommand = new SlashCommandBuilder();
                    guildCommand.WithName("dadjoke");
                    guildCommand.WithDescription("Get a dad joke");
                    try
                    {
                        await guild.CreateApplicationCommandAsync(guildCommand.Build());
                    }
                    catch (HttpException exception)
                    {
                        logger.LogError(exception, "Error creating command");
                    }
                }
                if (featureRuntimeCoordinator.IsEnabled(FeatureKeys.Weather))
                {
                    var guildCommand = new SlashCommandBuilder();
                    guildCommand.WithName("weather");
                    guildCommand.WithDescription("Get current weather");
                    guildCommand.AddOption("location", ApplicationCommandOptionType.String, "Location you would like to get weather for. Can be City, State, Zip, etc...");
                    try
                    {
                        await guild.CreateApplicationCommandAsync(guildCommand.Build());
                    }
                    catch (HttpException exception)
                    {
                        logger.LogError(exception, "Error creating command");
                    }
                }
                discordService.SetReady(true);
                logger.LogInformation("Discord Bot is ready.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in onReady");
            }
        }
    }
}
