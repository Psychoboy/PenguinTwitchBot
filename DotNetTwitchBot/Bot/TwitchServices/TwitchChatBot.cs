using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;

namespace DotNetTwitchBot.Bot.TwitchServices
{
    public class TwitchChatBot : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private TwitchClient _twitchClient { get; set; }
        private ServiceBackbone _eventService { get; set; }

        private TwitchService _twitchService;
        private TwitchBotService _twitchBotService;
        private readonly ILogger<TwitchChatBot> _logger;

        public TwitchChatBot(
            ILogger<TwitchChatBot> logger,
             IConfiguration configuration,
             ServiceBackbone eventService,
             TwitchBotService twitchBotService,
             TwitchService twitchService)
        {
            _configuration = configuration;
            _logger = logger;
            _twitchClient = new TwitchClient();
            _eventService = eventService;
            _twitchService = twitchService;
            _twitchBotService = twitchBotService;
            _eventService.SendMessageEvent += CommandService_OnSendMessage;
            _eventService.SendWhisperMessageEvent += CommandService_OnWhisperMessage;

        }



        private Task CommandService_OnSendMessage(object? sender, string e)
        {
            _twitchClient.SendMessage(_configuration["broadcaster"], e);
            _logger.LogInformation("BOTCHATMSG: {0}", e.Replace(Environment.NewLine, ""));
            return Task.CompletedTask;
        }

        private async Task CommandService_OnWhisperMessage(object? sender, string e, string e2)
        {
            //_twitchClient.SendWhisper(e, e2);
            await _twitchBotService.SendWhisper(e, e2);
            _logger.LogInformation("BOTWHISPERMSG: {0}", e.Replace(Environment.NewLine, "") + ": " + e2.Replace(Environment.NewLine, ""));
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
            _twitchClient.OnWhisperCommandReceived += OnWhisperCommandReceived;
            _twitchClient.OnWhisperReceived += OnWhisperReceived;
            _twitchClient.Connect();
        }



        private async void OnUserLeft(object? sender, OnUserLeftArgs e)
        {
            _logger.LogTrace("{0} Left.", e.Username);
            await _eventService.OnUserLeft(e.Username);
        }

        private async void OnUserJoined(object? sender, OnUserJoinedArgs e)
        {
            _logger.LogTrace("{0} Joined.", e.Username);
            await _eventService.OnUserJoined(e.Username);
        }

        private async void OnMessageReceived(object? sender, OnMessageReceivedArgs e)
        {
            _logger.LogInformation("CHATMSG: {0}: {1}", e.ChatMessage.Username, e.ChatMessage.Message);
            await _eventService.OnChatMessage(e.ChatMessage);
        }

        private void Client_OnConnectionError(object? sender, TwitchLib.Client.Events.OnConnectionErrorArgs e)
        {
            _logger.LogWarning("Bot Connection Error: {0}", e.Error.Message);
        }

        private void Client_OnMessageReceived(object? sender, TwitchLib.Client.Events.OnMessageReceivedArgs e)
        {
            _logger.LogTrace("OnMessageReceived");
        }

        private void OnWhisperReceived(object? sender, OnWhisperReceivedArgs e)
        {
            _logger.LogTrace("OnWhisperReceived");
        }

        private void Client_OnConnected(object? sender, TwitchLib.Client.Events.OnConnectedArgs e)
        {
            _logger.LogInformation("Bot Connected");
        }

        private void Client_OnLog(object? sender, TwitchLib.Client.Events.OnLogArgs e)
        {
            _logger.LogTrace("OnLog");
        }

        private void Client_OnError(object? sender, TwitchLib.Communication.Events.OnErrorEventArgs e)
        {
            _logger.LogError("Bot Error: {0}", e.Exception);
        }

        private void Client_OnDisconnected(object? sender, TwitchLib.Communication.Events.OnDisconnectedEventArgs e)
        {
            _logger.LogTrace("Bot Disconnected");
        }

        private async void Client_OnChatCommandReceived(object? sender, TwitchLib.Client.Events.OnChatCommandReceivedArgs e)
        {
            await _eventService.OnCommand(e.Command);
        }
        private async void OnWhisperCommandReceived(object? sender, OnWhisperCommandReceivedArgs e)
        {
            var command = new CommandEventArgs()
            {
                Arg = e.Command.ArgumentsAsString,
                Args = e.Command.ArgumentsAsList,
                Command = e.Command.CommandText.ToLower(),
                IsWhisper = true,
                Name = e.Command.WhisperMessage.Username,
                DisplayName = e.Command.WhisperMessage.DisplayName,
                isSub = await _twitchService.IsUserSub(e.Command.WhisperMessage.Username),
                isMod = await _twitchService.IsUserMod(e.Command.WhisperMessage.Username),
                TargetUser = e.Command.ArgumentsAsList.Count > 0
                        ? e.Command.ArgumentsAsList[0].Replace("@", "").Trim().ToLower()
                        : ""
            };
            await _eventService.OnWhisperCommand(command);
        }
        private void Client_OnLeftChannel(object? sender, TwitchLib.Client.Events.OnLeftChannelArgs e)
        {
            _logger.LogTrace("Bot left the channel ", e.Channel);
        }

        private async void Client_OnJoinedChannel(object? sender, TwitchLib.Client.Events.OnJoinedChannelArgs e)
        {
            _logger.LogInformation(string.Format("Joined {0}", e.Channel));
            try
            {
                _eventService.IsOnline = await _twitchService.IsStreamOnline();
                // await _twitchService.GetAllSubscriptions();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
            _logger.LogInformation("Stream Is Online: {IsOnline}", _eventService.IsOnline);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Run(() => Initialize());

        }
    }
}
