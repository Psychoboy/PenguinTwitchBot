using DotNetTwitchBot.Application.ChatMessage.Notifications;
using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Commands.Alias.Requests;
using DotNetTwitchBot.Bot.TwitchServices;
using KickLib.Client;
using KickLib.Client.Models.Args;
using KickLib.Client.Models.Events.Chatroom;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;

namespace DotNetTwitchBot.Bot.KickServices
{
    public partial class KickHostedService(
        ILogger<KickHostedService> logger,
        ChatMessageIdTracker chatMessageIdTracker,
        IMemoryCache memoryCache,
        IMediator mediator
        ) : IKickHostedService
    {
        private readonly KickClient kickClient = new();

        private void KickClient_OnUserUnbanned(object? sender, UserUnbannedEventArgs e)
        {
            logger.LogInformation("User unbanned: {Username}", e.Data.User.Username);
        }

        private void KickClient_OnUserBanned(object? sender, UserBannedEventArgs e)
        {
            logger.LogInformation("User banned: {Username}", e.Data.User.Username);
        }

        private void KickClient_OnSubscription(object? sender, SubscriptionEventArgs e)
        {
            logger.LogInformation("New subscription from: {Username}", e.Data.Username);
        }

        private void KickClient_OnStreamStatusChanged(object? sender, StreamStateChangedArgs e)
        {
            logger.LogInformation("Stream status changed: {Status}", e.Data.SessionTitle);
        }

        private void KickClient_OnStreamHost(object? sender, StreamHostEventArgs e)
        {
            logger.LogInformation("Stream hosted by: {HostUsername}", e.Data.HostUsername);
        }

        private void KickClient_OnRewardRedeemed(object? sender, RewardRedeemedEventArgs e)
        {
            logger.LogInformation("Reward redeemed: {RewardTitle} by {Username}", e.Data.RewardTitle, e.Data.Username);
        }

        private void KickClient_OnMessageDeleted(object? sender, MessageDeletedEventArgs e)
        {
            logger.LogInformation("Message deleted");
        }

        private void KickClient_OnMessage(object? sender, ChatMessageEventArgs e)
        {
            HandleMessage(e).Wait();
        }

        private async Task HandleMessage(ChatMessageEventArgs e)
        {
            if (e.Data.Metadata != null && chatMessageIdTracker.IsSelfMessage(e.Data.Metadata.Id))
            {
                return;
            }

            if (DidProcessMessage(e.Data.Metadata?.Id))
            {
                return;
            }

            var messageText = e.Data.Content;
            messageText = MessageRegex().Replace(messageText, string.Empty).Trim();

            logger.LogInformation("CHATMSG[K]: {name}: {message}", e.Data.Sender.Username, e.Data.Content);

            await Task.WhenAll([ProcessCommandMessage(e.Data), ProccessChatMessage(e.Data)]);
        }

        private Task ProccessChatMessage(ChatMessageEvent data)
        {
            var messageText = data.Content;
            messageText = MessageRegex().Replace(messageText, string.Empty).Trim();
            var chatMessage = new Events.Chat.ChatMessageEventArgs
            {
                Message = messageText,
                UserId = data.Sender.Id.ToString(),
                Name = data.Sender.Username.ToLower(),
                DisplayName = data.Sender.Username,
                IsSub = false,
                IsMod = false,
                IsVip = false,
                IsBroadcaster = false,
                MessageId = data.Id,
                FromOwnChannel = true,
                Platform = PlatformType.Kick
            };
            return mediator.Publish(new ReceivedChatMessage { EventArgs = chatMessage });
        }

        private async Task ProcessCommandMessage(ChatMessageEvent data)
        {
            if (data.Content.StartsWith('!') == false) return;

            var messageText = data.Content;
            messageText = MessageRegex().Replace(messageText, string.Empty).Trim();
            var argsFull = messageText.Split(' ', 2);
            var command = argsFull[0];
            var ArgumentsAsString = argsFull.Length > 1 ? argsFull[1] : "";
            var ArgumentsAsList = string.IsNullOrWhiteSpace(ArgumentsAsString) ? [] : ArgumentsAsString.Split(" ").ToList();
            var eventArgs = new Events.Chat.CommandEventArgs
            {
                Command = command[1..].ToLower(),
                Arg = ArgumentsAsString,
                Args = ArgumentsAsList,
                TargetUser = ArgumentsAsList.Count > 0 ? ArgumentsAsList[0].Replace("@", "").Trim() : "",
                IsWhisper = false,
                UserId = data.Sender.Id.ToString(),
                Name = data.Sender.Username.ToLower(),
                DisplayName = data.Sender.Username,
                IsSub = false,
                IsMod = false,
                IsVip = false,
                IsBroadcaster = false,
                MessageId = data.Id,
                FromOwnChannel = true,
                Platform = PlatformType.Kick

            };

            if (await mediator.Send(new AliasRunCommand { EventArgs = eventArgs }))
            {
                return;
            }

            await mediator.Publish(new RunCommandNotification { EventArgs = eventArgs });
        }

        private bool DidProcessMessage(string? id)
        {
            if(string.IsNullOrWhiteSpace(id))
            {
                return false;
            }
            if(memoryCache.TryGetValue(id, out _))
            {
                logger.LogWarning("Duplicate message detected: {MessageId}", id);
                return true;
            }
            memoryCache.Set(id, true, TimeSpan.FromMinutes(5));
            return false;
        }

        private void KickClient_OnKicksGifted(object? sender, KicksGiftedEventArgs e)
        {
            logger.LogInformation("Kicks gifted: {GifterUsername}", e.Data.Gift.Name);
        }

        private void KickClient_OnGiftsLeaderboardUpdated(object? sender, GiftsLeaderboardUpdatedArgs e)
        {
            logger.LogInformation("Gifts leaderboard updated.");
        }

        private void KickClient_OnGiftedSubscription(object? sender, GiftedSubscriptionsEventArgs e)
        {
            logger.LogInformation("Gifted subscription from: {GifterUsername}", e.Data.GifterUsername);
        }

        private void KickClient_OnFollowersUpdated(object? sender, FollowersUpdatedEventArgs e)
        {
            logger.LogInformation("Followers updated. Followed by {Username}", e.Data.Username);
        }

        private void KickClient_OnDisconnected(object? sender, EventArgs e)
        {
            logger.LogInformation("Kick client disconnected.");
        }

        private void KickClient_OnConnected(object? sender, ClientConnectedArgs e)
        {
            logger.LogInformation("Kick client connected.");
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            kickClient.OnConnected += KickClient_OnConnected;
            kickClient.OnDisconnected += KickClient_OnDisconnected;
            kickClient.OnFollowersUpdated += KickClient_OnFollowersUpdated;
            kickClient.OnGiftedSubscription += KickClient_OnGiftedSubscription;
            kickClient.OnGiftsLeaderboardUpdated += KickClient_OnGiftsLeaderboardUpdated;
            kickClient.OnKicksGifted += KickClient_OnKicksGifted;
            kickClient.OnMessage += KickClient_OnMessage;
            kickClient.OnMessageDeleted += KickClient_OnMessageDeleted;
            kickClient.OnRewardRedeemed += KickClient_OnRewardRedeemed;
            kickClient.OnStreamHost += KickClient_OnStreamHost;
            kickClient.OnStreamStatusChanged += KickClient_OnStreamStatusChanged;
            kickClient.OnSubscription += KickClient_OnSubscription;
            kickClient.OnUserBanned += KickClient_OnUserBanned;
            kickClient.OnUserUnbanned += KickClient_OnUserUnbanned;

            var chatroomId = 1343485;
            var channelId = 1350579;
            await kickClient.ListenToChatRoomAsync(chatroomId);
            await kickClient.ListenToChannelAsync(channelId);
            await kickClient.ConnectAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            kickClient.OnConnected -= KickClient_OnConnected;
            kickClient.OnDisconnected -= KickClient_OnDisconnected;
            kickClient.OnFollowersUpdated -= KickClient_OnFollowersUpdated;
            kickClient.OnGiftedSubscription -= KickClient_OnGiftedSubscription;
            kickClient.OnGiftsLeaderboardUpdated -= KickClient_OnGiftsLeaderboardUpdated;
            kickClient.OnKicksGifted -= KickClient_OnKicksGifted;
            kickClient.OnMessage -= KickClient_OnMessage;
            kickClient.OnMessageDeleted -= KickClient_OnMessageDeleted;
            kickClient.OnRewardRedeemed -= KickClient_OnRewardRedeemed;
            kickClient.OnStreamHost -= KickClient_OnStreamHost;
            kickClient.OnStreamStatusChanged -= KickClient_OnStreamStatusChanged;
            kickClient.OnSubscription -= KickClient_OnSubscription;
            kickClient.OnUserBanned -= KickClient_OnUserBanned;
            kickClient.OnUserUnbanned -= KickClient_OnUserUnbanned;

            await kickClient.DisconnectAsync();
        }
        [GeneratedRegex(@"[^\u0000-\u00FF]+")]
        private static partial Regex MessageRegex();
    }
}
