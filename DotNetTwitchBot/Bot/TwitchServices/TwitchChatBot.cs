using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
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
         ITwitchService twitchService) : IHostedService
    {
        private TwitchClient TwitchClient { get; set; } = default!;

        private readonly Timer HealthStatusTimer = new();

        public bool IsConnected()
        {
            return TwitchClient.IsConnected;
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
            await TwitchClient.SendMessageAsync(configuration["broadcaster"] ?? "", e);
            logger.LogInformation("BOTCHATMSG: {message}", e.Replace(Environment.NewLine, ""));
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

        private async Task OnMessageReceived(object? sender, OnMessageReceivedArgs e)
        {
            logger.LogInformation("CHATMSG: {name}: {message}", e.ChatMessage.Username, e.ChatMessage.Message);
            if (e.ChatMessage != null)
            {
                var chatMessage = new ChatMessageEventArgs
                {
                    Message = e.ChatMessage.Message,
                    Name = e.ChatMessage.Username.ToLower(),
                    DisplayName = e.ChatMessage.DisplayName,
                    IsSub = e.ChatMessage.IsSubscriber,
                    IsMod = e.ChatMessage.IsModerator,
                    IsVip = e.ChatMessage.IsVip,
                    IsBroadcaster = e.ChatMessage.IsBroadcaster
                };
                await serviceBackbone.OnChatMessage(chatMessage);
            }
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

        private Task OnWhisperReceived(object? sender, OnWhisperReceivedArgs e)
        {
            return Task.Run(() => logger.LogTrace("OnWhisperReceived"));
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

        private Task Client_OnDisconnected(object? sender, TwitchLib.Communication.Events.OnDisconnectedEventArgs e)
        {
            return Task.Run(() => logger.LogTrace("Bot Disconnected"));
        }

        private async Task Client_OnChatCommandReceived(object? sender, TwitchLib.Client.Events.OnChatCommandReceivedArgs e)
        {
            var eventArgs = new CommandEventArgs
            {
                Arg = e.Command.ArgumentsAsString,
                Args = e.Command.ArgumentsAsList.Where(x => string.IsNullOrEmpty(x) == false).ToList(),
                Command = e.Command.Name.ToLower(),
                IsWhisper = false,
                Name = e.ChatMessage.Username,
                DisplayName = e.ChatMessage.DisplayName,
                IsSub = e.ChatMessage.IsSubscriber,
                IsMod = e.ChatMessage.IsBroadcaster || e.ChatMessage.IsModerator,
                IsVip = e.ChatMessage.IsVip,
                IsBroadcaster = e.ChatMessage.IsBroadcaster,
                TargetUser = e.Command.ArgumentsAsList.Count > 0
                        ? e.Command.ArgumentsAsList[0].Replace("@", "").Trim().ToLower()
                        : ""
            };
            await serviceBackbone.OnCommand(eventArgs);
        }
        private async Task OnWhisperCommandReceived(object? sender, OnWhisperCommandReceivedArgs e)
        {
            var command = new CommandEventArgs()
            {
                Arg = e.Command.ArgumentsAsString,
                Args = e.Command.ArgumentsAsList,
                Command = e.Command.Name.ToLower(),
                IsWhisper = true,
                Name = e.WhisperMessage.Username,
                DisplayName = e.WhisperMessage.DisplayName,
                IsSub = await twitchService.IsUserSub(e.WhisperMessage.Username),
                IsMod = await twitchService.IsUserMod(e.WhisperMessage.Username),
                TargetUser = e.Command.ArgumentsAsList.Count > 0
                        ? e.Command.ArgumentsAsList[0].Replace("@", "").Trim().ToLower()
                        : ""
            };
            await serviceBackbone.OnWhisperCommand(command);
        }
        private Task Client_OnLeftChannel(object? sender, TwitchLib.Client.Events.OnLeftChannelArgs e)
        {
            return Task.Run(() => logger.LogTrace("Bot left the channel {channe}", e.Channel));
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

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            TwitchClient = new TwitchClient();
            serviceBackbone.SendMessageEvent += CommandService_OnSendMessage;
            HealthStatusTimer.Interval = 30000;
            HealthStatusTimer.Elapsed += HealthStatusTimer_Elapsed;
            var credentials = new ConnectionCredentials(configuration["botName"] ?? "", configuration["botTwitchOAuth"] ?? "");
            TwitchClient.Initialize(credentials, configuration["broadcaster"]);
            TwitchClient.OnJoinedChannel += Client_OnJoinedChannel;
            TwitchClient.OnLeftChannel += Client_OnLeftChannel;
            TwitchClient.OnChatCommandReceived += Client_OnChatCommandReceived;
            TwitchClient.OnDisconnected += Client_OnDisconnected;
            TwitchClient.OnError += Client_OnError;
            TwitchClient.OnConnected += Client_OnConnected;
            TwitchClient.OnConnectionError += Client_OnConnectionError;
            TwitchClient.OnMessageReceived += OnMessageReceived;
            TwitchClient.OnUserJoined += OnUserJoined;
            TwitchClient.OnUserLeft += OnUserLeft;
            TwitchClient.OnWhisperCommandReceived += OnWhisperCommandReceived;
            TwitchClient.OnWhisperReceived += OnWhisperReceived;
            TwitchClient.OnReconnected += OnReconnected;
            await TwitchClient.ConnectAsync();
            HealthStatusTimer.Start();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await TwitchClient.DisconnectAsync();
        }
    }
}
