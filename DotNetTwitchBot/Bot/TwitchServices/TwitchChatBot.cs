using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using Timer = System.Timers.Timer;

namespace DotNetTwitchBot.Bot.TwitchServices
{
    public class TwitchChatBot
    {
        private readonly IConfiguration _configuration;
        private TwitchClient TwitchClient { get; set; }
        private IServiceBackbone EventService { get; set; }

        private readonly ITwitchService _twitchService;
        private readonly ITwitchBotService _twitchBotService;
        private readonly ILogger<TwitchChatBot> _logger;
        private readonly Timer HealthStatusTimer = new();

        public TwitchChatBot(
            ILogger<TwitchChatBot> logger,
             IConfiguration configuration,
             IServiceBackbone eventService,
             ITwitchBotService twitchBotService,
             ITwitchService twitchService)
        {
            _configuration = configuration;
            _logger = logger;
            TwitchClient = new TwitchClient();
            EventService = eventService;
            _twitchService = twitchService;
            _twitchBotService = twitchBotService;
            EventService.SendMessageEvent += CommandService_OnSendMessage;
            EventService.SendWhisperMessageEvent += CommandService_OnWhisperMessage;
            HealthStatusTimer.Interval = 30000;
            HealthStatusTimer.Elapsed += HealthStatusTimer_Elapsed;

        }

        public bool IsConnected()
        {
            return TwitchClient.IsConnected;
        }

        public bool IsInChannel()
        {
            return (TwitchClient.JoinedChannels.Where(x => x.Channel.Equals(_configuration["broadcaster"], StringComparison.OrdinalIgnoreCase)).Any() == true);
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

        private Task CommandService_OnSendMessage(object? sender, string e)
        {
            TwitchClient.SendMessage(_configuration["broadcaster"], e);
            _logger.LogInformation("BOTCHATMSG: {message}", e.Replace(Environment.NewLine, ""));
            return Task.CompletedTask;
        }

        private async Task CommandService_OnWhisperMessage(object? sender, string e, string e2)
        {
            await _twitchBotService.SendWhisper(e, e2);
            _logger.LogInformation("BOTWHISPERMSG: {message}", e.Replace(Environment.NewLine, "") + ": " + e2.Replace(Environment.NewLine, ""));
        }

        public async Task Initialize()
        {
            var credentials = new ConnectionCredentials(_configuration["botName"], _configuration["botTwitchOAuth"]);
            TwitchClient.Initialize(credentials, _configuration["broadcaster"]);
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

        private Task OnReconnected(object? sender, OnConnectedEventArgs e)
        {
            return Task.Run(() => _logger.LogInformation("Bot reconnected"));
        }

        private async Task OnUserLeft(object? sender, OnUserLeftArgs e)
        {
            _logger.LogTrace("{name} Left.", e.Username);
            await EventService.OnUserLeft(e.Username);
        }

        private async Task OnUserJoined(object? sender, OnUserJoinedArgs e)
        {
            _logger.LogTrace("{name} Joined.", e.Username);
            await EventService.OnUserJoined(e.Username);
        }

        private async Task OnMessageReceived(object? sender, OnMessageReceivedArgs e)
        {
            _logger.LogInformation("CHATMSG: {name}: {message}", e.ChatMessage.Username, e.ChatMessage.Message);
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
                await EventService.OnChatMessage(chatMessage);
            }
        }

        private Task Client_OnConnectionError(object? sender, TwitchLib.Client.Events.OnConnectionErrorArgs e)
        {
            _logger.LogWarning("Bot Connection Error, will reconnect in about 5 seconds: {error}", e.Error.Message);
            Thread.Sleep(5000);
            if (TwitchClient.IsConnected == false)
            {
                _logger.LogInformation("Reconnecting Twitch Client");
                return TwitchClient.ReconnectAsync();
            }
            else
            {
                _logger.LogInformation("Twitch Client was already connected so continuing");
                return Task.CompletedTask;
            }
        }

        private Task OnWhisperReceived(object? sender, OnWhisperReceivedArgs e)
        {
            return Task.Run(() => _logger.LogTrace("OnWhisperReceived"));
        }

        private Task Client_OnConnected(object? sender, TwitchLib.Client.Events.OnConnectedEventArgs e)
        {
            return Task.Run(() => _logger.LogInformation("Bot Connected"));
        }

        private async Task JoinChannelIfNotJoined()
        {
            if (TwitchClient.JoinedChannels.Where(x => x.Channel.Equals(_configuration["broadcaster"], StringComparison.OrdinalIgnoreCase)).Any() == false)
            {
                _logger.LogWarning("Chat Bot was not in the channel, re-joining...");
                await TwitchClient.JoinChannelAsync(_configuration["broadcaster"]);
            }
        }

        private Task Client_OnError(object? sender, TwitchLib.Communication.Events.OnErrorEventArgs e)
        {
            return Task.Run(() => _logger.LogError("Bot Error: {error}", e.Exception));
        }

        private Task Client_OnDisconnected(object? sender, TwitchLib.Communication.Events.OnDisconnectedEventArgs e)
        {
            return Task.Run(() => _logger.LogTrace("Bot Disconnected"));
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
            await EventService.OnCommand(eventArgs);
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
                IsSub = await _twitchService.IsUserSub(e.WhisperMessage.Username),
                IsMod = await _twitchService.IsUserMod(e.WhisperMessage.Username),
                TargetUser = e.Command.ArgumentsAsList.Count > 0
                        ? e.Command.ArgumentsAsList[0].Replace("@", "").Trim().ToLower()
                        : ""
            };
            await EventService.OnWhisperCommand(command);
        }
        private Task Client_OnLeftChannel(object? sender, TwitchLib.Client.Events.OnLeftChannelArgs e)
        {
            return Task.Run(() => _logger.LogTrace("Bot left the channel ", e.Channel));
        }

        private async Task Client_OnJoinedChannel(object? sender, TwitchLib.Client.Events.OnJoinedChannelArgs e)
        {
            //Restart timer
            HealthStatusTimer.Stop();
            HealthStatusTimer.Start();
            _logger.LogInformation("Bot Joined {Channel}", e.Channel);
            try
            {
                EventService.IsOnline = await _twitchService.IsStreamOnline();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error Checking if stream is online.");
            }
            _logger.LogInformation("Stream Online: {IsOnline}", EventService.IsOnline);
        }
    }
}
