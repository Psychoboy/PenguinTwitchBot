using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Core;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;

namespace DotNetTwitchBot.Bot
{
    public class TwitchChatBot : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private TwitchClient _twitchClient { get; set; }
        private EventService _eventService { get; set;}

        private TwitchService _twitchService;
        private readonly ILogger<TwitchChatBot> _logger;

        public TwitchChatBot(
            ILogger<TwitchChatBot> logger,
             IConfiguration configuration, 
             EventService eventService,
             TwitchService twitchService) {
            _configuration = configuration;
            _logger = logger;
            _twitchClient = new TwitchClient();
            _eventService = eventService;
            _twitchService = twitchService;
            _eventService.SendMessageEvent += CommandService_OnSendMessage;
            
        }

        private Task CommandService_OnSendMessage(object? sender, string e)
        {
            _twitchClient.SendMessage(_configuration["broadcaster"], e);
            return Task.CompletedTask;
        }

        public void Initialize() 
        {
            var credentials = new ConnectionCredentials(_configuration["botName"], _configuration["botTwitchOAuth"]);
            _twitchClient.Initialize(credentials, _configuration["broadcaster"]);
            _twitchClient.OnJoinedChannel += Client_OnJoinedChannel;
            _twitchClient.OnLeftChannel += Client_OnLeftChannel;
            _twitchClient.OnChatCommandReceived += Client_OnChatCommandReceived;
            _twitchClient.OnDisconnected += Client_OnDisconnected;
            _twitchClient.OnError += Client_OnError;
            _twitchClient.OnLog += Client_OnLog;
            _twitchClient.OnConnected += Client_OnConnected;
            _twitchClient.OnMessageReceived += Client_OnMessageReceived;
            _twitchClient.OnConnectionError += Client_OnConnectionError;
            _twitchClient.OnMessageReceived += OnMessageReceived;
            _twitchClient.OnUserJoined += OnUserJoined;
            _twitchClient.OnUserLeft += OnUserLeft;
            _twitchClient.Connect();
        }

        private void OnUserLeft(object? sender, OnUserLeftArgs e)
        {
            _logger.LogInformation("{0} Left.", e.Username);
        }

        private void OnUserJoined(object? sender, OnUserJoinedArgs e)
        {
            _logger.LogInformation("{0} Joined.", e.Username);
        }

        private async void OnMessageReceived(object? sender, OnMessageReceivedArgs e)
        {
            await _eventService.OnChatMessage(e.ChatMessage);
        }

        private void Client_OnConnectionError(object? sender, TwitchLib.Client.Events.OnConnectionErrorArgs e)
        {
            _logger.LogWarning("Bot Connection Error: {0}", e.Error.Message);
        }

        private void Client_OnMessageReceived(object? sender, TwitchLib.Client.Events.OnMessageReceivedArgs e)
        {
            _logger.LogDebug("OnMessageReceived");
        }

        private void Client_OnConnected(object? sender, TwitchLib.Client.Events.OnConnectedArgs e)
        {
            _logger.LogInformation("Bot Connected");
        }

        private void Client_OnLog(object? sender, TwitchLib.Client.Events.OnLogArgs e)
        {
            _logger.LogDebug("OnLog");
        }

        private void Client_OnError(object? sender, TwitchLib.Communication.Events.OnErrorEventArgs e)
        {
            _logger.LogError("Bot Error: {0}", e.Exception);
        }

        private void Client_OnDisconnected(object? sender, TwitchLib.Communication.Events.OnDisconnectedEventArgs e)
        {
            _logger.LogDebug("Bot Disconnected");
        }

        private async void Client_OnChatCommandReceived(object? sender, TwitchLib.Client.Events.OnChatCommandReceivedArgs e)
        {
            await _eventService.OnCommand(e.Command);
        }

        private void Client_OnLeftChannel(object? sender, TwitchLib.Client.Events.OnLeftChannelArgs e)
        {
            _logger.LogDebug("Bot left the channel ",e.Channel);
        }

        private async void Client_OnJoinedChannel(object? sender, TwitchLib.Client.Events.OnJoinedChannelArgs e)
        {
            _logger.LogInformation(string.Format("Joined {0}", e.Channel));
            try{
                _eventService.IsOnline = await _twitchService.IsStreamOnline();
                // await _twitchService.GetAllSubscriptions();
            } catch (Exception ex) {
                _logger.LogError(ex.Message);
            }
            _logger.LogInformation($"Stream Is Online: {_eventService.IsOnline}");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Run(() => Initialize());
            
        }
    }
}
