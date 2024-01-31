using DotNetTwitchBot.Bot.Core;
using System.Timers;
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using Timer = System.Timers.Timer;

namespace DotNetTwitchBot.Bot.TwitchServices
{
    public class TwitchChatBot(
        ILogger<TwitchChatBot> logger,
         IConfiguration configuration,
         IServiceBackbone serviceBackbone,
         ITwitchService twitchService,
         SettingsFileManager settingsFileManager) : IHostedService, ITwitchChatBot
    {
        private TwitchClient TwitchClient { get; set; } = default!;
        private readonly TwitchAPI _twitchApi = new();

        private readonly Timer HealthStatusTimer = new();
        static readonly SemaphoreSlim semaphoreSlim = new(1, 1);
        readonly Timer _timer = new(300000); //5 minutes;

        public bool IsConnected()
        {
            return TwitchClient.IsConnected;
        }

        private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            await ValidateAndRefreshToken();
        }

        public bool IsInChannel()
        {
            return (TwitchClient.JoinedChannels.Where(x => x.Channel.Equals(configuration["broadcaster"], StringComparison.OrdinalIgnoreCase)).Any() == true);
        }

        private async void HealthStatusTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (!TwitchClient.IsConnected)
            {
                // Wait a few seconds before trying to reconnect
                Thread.Sleep(5000);
                if (!TwitchClient.IsConnected)
                {
                    await TwitchClient.ConnectAsync();
                    return;
                }
            }
            await JoinChannelIfNotJoined();
        }

        private async Task CommandService_OnSendMessage(object? sender, string e)
        {
            var result = await _twitchApi.Helix.Chat.SendChatMessage(await twitchService.GetBroadcasterUserId(), await twitchService.GetBotUserId(), e);
            if (result.Data.First().IsSent == false)
            {
                logger.LogWarning("Message failed to send: {reason}", result.Data.First().DropReason.FirstOrDefault()?.Message);
            }
            else
            {
                logger.LogInformation("BOTCHATMSG: {message}", e.Replace(Environment.NewLine, ""));
            }
        }

        private Task OnReconnected(object? sender, OnConnectedEventArgs e)
        {
            return Task.Run(() => logger.LogInformation("Bot reconnected"));
        }

        private async Task OnUserLeft(object? sender, OnUserLeftArgs e)
        {
            logger.LogTrace("{name} Left.", e.Username);
            await serviceBackbone.OnUserLeft(e.Username);
        }

        private async Task OnUserJoined(object? sender, OnUserJoinedArgs e)
        {
            logger.LogTrace("{name} Joined.", e.Username);
            await serviceBackbone.OnUserJoined(e.Username);
        }

        private Task Client_OnConnectionError(object? sender, TwitchLib.Client.Events.OnConnectionErrorArgs e)
        {
            logger.LogWarning("Bot Connection Error, will reconnect in about 5 seconds: {error}", e.Error.Message);
            Thread.Sleep(5000);
            if (TwitchClient.IsConnected == false)
            {
                logger.LogInformation("Reconnecting Twitch Client");
                return TwitchClient.ReconnectAsync();
            }
            else
            {
                logger.LogInformation("Twitch Client was already connected so continuing");
                return Task.CompletedTask;
            }
        }

        private Task Client_OnConnected(object? sender, TwitchLib.Client.Events.OnConnectedEventArgs e)
        {
            return Task.Run(() => logger.LogInformation("Bot Connected"));
        }

        private async Task JoinChannelIfNotJoined()
        {
            if (TwitchClient.JoinedChannels.Where(x => x.Channel.Equals(configuration["broadcaster"], StringComparison.OrdinalIgnoreCase)).Any() == false)
            {
                logger.LogWarning("Chat Bot was not in the channel, re-joining...");
                await TwitchClient.JoinChannelAsync(configuration["broadcaster"] ?? "");
            }
        }

        private Task Client_OnError(object? sender, TwitchLib.Communication.Events.OnErrorEventArgs e)
        {
            return Task.Run(() => logger.LogError("Bot Error: {error}", e.Exception));
        }


        private async Task Client_OnJoinedChannel(object? sender, TwitchLib.Client.Events.OnJoinedChannelArgs e)
        {
            //Restart timer
            HealthStatusTimer.Stop();
            HealthStatusTimer.Start();
            logger.LogInformation("Bot Joined {Channel}", e.Channel);
            try
            {
                serviceBackbone.IsOnline = await twitchService.IsStreamOnline();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error Checking if stream is online.");
            }
            logger.LogInformation("Stream Online: {IsOnline}", serviceBackbone.IsOnline);
        }

        public async Task ValidateAndRefreshToken()
        {
            await semaphoreSlim.WaitAsync();
            try
            {
                var validToken = await _twitchApi.Auth.ValidateAccessTokenAsync(configuration["twitchBotAccessToken"]);
                if (validToken != null && validToken.ExpiresIn > 1200)
                {
                    TimeSpan.FromSeconds(validToken.ExpiresIn);
                    await settingsFileManager.AddOrUpdateAppSetting("botExpiresIn", validToken.ExpiresIn);
                }
                else
                {
                    try
                    {
                        logger.LogInformation("Refreshing Bot Token");

                        var refreshToken = await _twitchApi.Auth.RefreshAuthTokenAsync(configuration["twitchBotRefreshToken"], configuration["twitchBotClientSecret"], configuration["twitchBotClientId"]);
                        configuration["twitchBotAccessToken"] = refreshToken.AccessToken;
                        configuration["botExpiresIn"] = refreshToken.ExpiresIn.ToString();
                        configuration["twitchBotRefreshToken"] = refreshToken.RefreshToken;
                        _twitchApi.Settings.AccessToken = refreshToken.AccessToken;
                        await settingsFileManager.AddOrUpdateAppSetting("twitchBotAccessToken", refreshToken.AccessToken);
                        await settingsFileManager.AddOrUpdateAppSetting("twitchBotRefreshToken", refreshToken.RefreshToken);
                        await settingsFileManager.AddOrUpdateAppSetting("botExpiresIn", refreshToken.ExpiresIn.ToString());
                    }
                    catch (Exception e)
                    {
                        logger.LogError("Error refreshing bot token: {error}", e.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error when validing/refreshing token");
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            TwitchClient = new TwitchClient();
            HealthStatusTimer.Interval = 30000;
            HealthStatusTimer.Elapsed += HealthStatusTimer_Elapsed;
            var credentials = new ConnectionCredentials(configuration["botName"] ?? "", configuration["botTwitchOAuth"] ?? "");
            TwitchClient.Initialize(credentials, configuration["broadcaster"]);
            TwitchClient.OnJoinedChannel += Client_OnJoinedChannel;
            TwitchClient.OnError += Client_OnError;
            TwitchClient.OnConnected += Client_OnConnected;
            TwitchClient.OnConnectionError += Client_OnConnectionError;
            TwitchClient.OnUserJoined += OnUserJoined;
            TwitchClient.OnUserLeft += OnUserLeft;
            TwitchClient.OnReconnected += OnReconnected;
            await TwitchClient.ConnectAsync();

            _twitchApi.Settings.ClientId = configuration["twitchBotClientId"];
            _twitchApi.Settings.AccessToken = configuration["twitchBotAccessToken"];
            _twitchApi.Settings.Scopes = [];
            foreach (var authScope in Enum.GetValues(typeof(AuthScopes)))
            {
                if ((AuthScopes)authScope == AuthScopes.Any) continue;
                _twitchApi.Settings.Scopes.Add((AuthScopes)authScope);
            }

            _timer.Elapsed += OnTimerElapsed;
            _timer.Start();
            await ValidateAndRefreshToken();
            HealthStatusTimer.Start();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await TwitchClient.DisconnectAsync();
        }
    }
}
