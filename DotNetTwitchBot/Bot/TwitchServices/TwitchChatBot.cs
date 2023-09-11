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
        private TwitchClient TwitchClient { get; set; }
        private IServiceBackbone EventService { get; set; }

        private readonly ITwitchService _twitchService;
        private readonly TwitchBotService _twitchBotService;
        private readonly ILogger<TwitchChatBot> _logger;

        public TwitchChatBot(
            ILogger<TwitchChatBot> logger,
             IConfiguration configuration,
             IServiceBackbone eventService,
             TwitchBotService twitchBotService,
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

        }



        private Task CommandService_OnSendMessage(object? sender, string e)
        {
            TwitchClient.SendMessage(_configuration["broadcaster"], e);
            _logger.LogInformation("BOTCHATMSG: {0}", e.Replace(Environment.NewLine, ""));
            return Task.CompletedTask;
        }

        private async Task CommandService_OnWhisperMessage(object? sender, string e, string e2)
        {
            await _twitchBotService.SendWhisper(e, e2);
            _logger.LogInformation("BOTWHISPERMSG: {0}", e.Replace(Environment.NewLine, "") + ": " + e2.Replace(Environment.NewLine, ""));
        }

        public Task Initialize()
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
            return TwitchClient.ConnectAsync();
        }



        private async Task OnUserLeft(object? sender, OnUserLeftArgs e)
        {
            _logger.LogTrace("{0} Left.", e.Username);
            await EventService.OnUserLeft(e.Username);
        }

        private async Task OnUserJoined(object? sender, OnUserJoinedArgs e)
        {
            _logger.LogTrace("{0} Joined.", e.Username);
            await EventService.OnUserJoined(e.Username);
        }

        private async Task OnMessageReceived(object? sender, OnMessageReceivedArgs e)
        {
            _logger.LogInformation("CHATMSG: {0}: {1}", e.ChatMessage.Username, e.ChatMessage.Message);
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
            return Task.Run(() => _logger.LogWarning("Bot Connection Error: {0}", e.Error.Message));
        }

        private Task OnWhisperReceived(object? sender, OnWhisperReceivedArgs e)
        {
            return Task.Run(() => _logger.LogTrace("OnWhisperReceived"));
        }

        private Task Client_OnConnected(object? sender, TwitchLib.Client.Events.OnConnectedEventArgs e)
        {
            return Task.Run(() => _logger.LogInformation("Bot Connected"));
        }

        private Task Client_OnError(object? sender, TwitchLib.Communication.Events.OnErrorEventArgs e)
        {
            return Task.Run(() => _logger.LogError("Bot Error: {0}", e.Exception));
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
            _logger.LogInformation(string.Format("Joined {0}", e.Channel));
            try
            {
                EventService.IsOnline = await _twitchService.IsStreamOnline();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
            _logger.LogInformation("Stream Is Online: {IsOnline}", EventService.IsOnline);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Initialize();

        }
    }
}
