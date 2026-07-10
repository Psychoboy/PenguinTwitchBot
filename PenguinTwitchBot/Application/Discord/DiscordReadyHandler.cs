using Discord;
using Discord.Net;
using Discord.WebSocket;
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
        private DiscordSocketClient? _client;
        private readonly SemaphoreSlim _weatherSyncLock = new(1, 1);
        private bool _featureStateSubscribed;
        private bool? _lastWeatherEnabled;

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
                _client = notification.Client;
                if (!_featureStateSubscribed)
                {
                    featureRuntimeCoordinator.StateChangedAsync += HandleFeatureStateChangedAsync;
                    _featureStateSubscribed = true;
                }

                await RefreshWeatherCommandsAsync(notification.Client, cancellationToken);
                _lastWeatherEnabled = featureRuntimeCoordinator.IsEnabled(FeatureKeys.Weather);

                discordService.SetReady(true);
                logger.LogInformation("Discord Bot is ready.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in onReady");
            }
        }

        private async Task HandleFeatureStateChangedAsync()
        {
            var client = _client;
            if (client == null)
            {
                return;
            }

            var isWeatherEnabled = featureRuntimeCoordinator.IsEnabled(FeatureKeys.Weather);
            if (_lastWeatherEnabled.HasValue && _lastWeatherEnabled.Value == isWeatherEnabled)
            {
                return;
            }

            _lastWeatherEnabled = isWeatherEnabled;
            await RefreshWeatherCommandsAsync(client, CancellationToken.None);
        }

        private async Task RefreshWeatherCommandsAsync(DiscordSocketClient client, CancellationToken cancellationToken)
        {
            await _weatherSyncLock.WaitAsync(cancellationToken);
            try
            {
                var settings = configuration.GetSection("Discord").Get<DiscordSettings>();
                if (settings == null || settings.DiscordServerId == 0)
                {
                    return;
                }

                var guild = client.GetGuild(settings.DiscordServerId);
                if (guild == null)
                {
                    return;
                }

                var weatherEnabled = featureRuntimeCoordinator.IsEnabled(FeatureKeys.Weather);
                await SyncWeatherCommandAsync(guild, weatherEnabled);
            }
            finally
            {
                _weatherSyncLock.Release();
            }
        }

        private async Task SyncWeatherCommandAsync(IGuild guild, bool enabled)
        {
            var existingWeatherCommand = (await guild.GetApplicationCommandsAsync())
                .FirstOrDefault(command => command.Name.Equals("weather", StringComparison.OrdinalIgnoreCase));

            if (!enabled)
            {
                if (existingWeatherCommand != null)
                {
                    try
                    {
                        await existingWeatherCommand.DeleteAsync();
                    }
                    catch (HttpException exception)
                    {
                        logger.LogError(exception, "Error deleting command");
                    }
                }

                return;
            }

            if (existingWeatherCommand != null)
            {
                try
                {
                    await existingWeatherCommand.DeleteAsync();
                }
                catch (HttpException exception)
                {
                    logger.LogError(exception, "Error deleting command");
                }
            }

            try
            {
                await guild.CreateApplicationCommandAsync(BuildWeatherCommand().Build());
            }
            catch (HttpException exception)
            {
                logger.LogError(exception, "Error creating command");
            }
        }

        private static SlashCommandBuilder BuildWeatherCommand()
        {
            var guildCommand = new SlashCommandBuilder();
            guildCommand.WithName("weather");
            guildCommand.WithDescription("Get current weather");
            guildCommand.AddOption("location", ApplicationCommandOptionType.String, "Location you would like to get weather for. Can be City, State, Zip, etc...");
            return guildCommand;
        }
    }
}
