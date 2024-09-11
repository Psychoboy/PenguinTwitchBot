using DotNetTwitchBot.Application.ChatMessage.Notification;
using DotNetTwitchBot.Application.ChatMessage.Notifications;
using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Commands.Alias.Requests;
using DotNetTwitchBot.Bot.Commands.Moderation;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Hubs;
using DotNetTwitchBot.Bot.Notifications;
using DotNetTwitchBot.CustomMiddleware;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;

namespace DotNetTwitchBot.Bot.Core
{
    public class ServiceBackbone(
        ILogger<ServiceBackbone> logger,
        IMediator mediator,
        IKnownBots knownBots,
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        IHubContext<MainHub> hubContext) : IServiceBackbone
    {
        static readonly SemaphoreSlim _semaphoreSlim = new(1);
        public bool HealthStatus { get; private set; } = true;
        private string? RawBroadcasterName { get; set; } = configuration["broadcaster"];
        public string? BotName { get; set; } = configuration["botName"];

        public event AsyncEventHandler<AdBreakStartEventArgs>? AdBreakStartEvent;
        public event AsyncEventHandler<CommandEventArgs>? CommandEvent;
        public event AsyncEventHandler<CheerEventArgs>? CheerEvent;
        public event AsyncEventHandler<FollowEventArgs>? FollowEvent;
        public event AsyncEventHandler<SubscriptionEventArgs>? SubscriptionEvent;
        public event AsyncEventHandler<SubscriptionGiftEventArgs>? SubscriptionGiftEvent;
        public event AsyncEventHandler<SubscriptionEndEventArgs>? SubscriptionEndEvent;
        public event AsyncEventHandler<ChannelPointRedeemEventArgs>? ChannelPointRedeemEvent;
        public event AsyncEventHandler<UserJoinedEventArgs>? UserJoinedEvent;
        public event AsyncEventHandler<UserLeftEventArgs>? UserLeftEvent;
        public event AsyncEventHandler<RaidEventArgs>? IncomingRaidEvent;
        public event AsyncEventHandler<BanEventArgs>? BanEvent;
        public event AsyncEventHandler? StreamStarted;
        public event AsyncEventHandler? StreamEnded;
        public bool IsOnline { get; set; } = false;
        public string BroadcasterName { get { return RawBroadcasterName ?? ""; } }
        public bool IsBroadcasterOrBot(string name)
        {
            return knownBots.IsStreamerOrBot(name);
        }

        public bool IsKnownBot(string name)
        {
            return knownBots.IsKnownBot(name);
        }

        public bool IsKnownBotOrCurrentStreamer(string name)
        {
            return knownBots.IsKnownBotOrCurrentStreamer(name);
        }

        public async Task RunCommand(CommandEventArgs eventArgs)
        {
            if (await mediator.Send(new AliasRunCommand { EventArgs = eventArgs }))
            {
                return;
            }

            await mediator.Publish(new RunCommandNotification { EventArgs = eventArgs });
        }

        public async Task OnCommand(CommandEventArgs command)
        {
            if (command != null)
            {
                await RunCommand(command);
            }
        }

        private static List<string> AllowedWhisperCommands
        {
            get
            {
                return
                [
                    "entries"
                ];
            }
        }

        public async Task OnWhisperCommand(CommandEventArgs command)
        {
            if (CommandEvent != null)
            {
                if (IsBroadcasterOrBot(command.Name) || AllowedWhisperCommands.Contains(command.Command))
                {
                    command.IsBroadcaster = IsBroadcasterOrBot(command.Name);
                    try
                    {
                        await CommandEvent(this, command);
                    }
                    catch (Exception e)
                    {
                        logger.LogCritical("Whisper Failure {ex}", e);
                    }
                }
            }
        }

        public Task SendChatMessage(string message)
        {
            return mediator.Publish(new SendBotMessage(message));
        }

        public async Task SendChatMessage(string name, string message)
        {
            await SendChatMessage(string.Format("@{0}, {1}", name, message));
        }
        public async Task SendChatMessageWithTitle(string viewerName, string message)
        {
            using var scope = scopeFactory.CreateAsyncScope();
            var viewerService = scope.ServiceProvider.GetRequiredService<Commands.Features.IViewerFeature>();
            var nameWithTitle = await viewerService.GetNameWithTitle(viewerName);
            await SendChatMessage(string.Format("{0}, {1}", string.IsNullOrWhiteSpace(nameWithTitle) ? viewerName : nameWithTitle, message));
        }

        public async Task OnStreamStarted()
        {
            await mediator.Publish(new StreamStartedNotification());

            await hubContext.Clients.All.SendAsync("StreamChanged", true);

            if (StreamStarted != null)
            {
                try
                {
                    await StreamStarted(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error firing StreamStarted");
                }
            }
        }

        public async Task OnStreamEnded()
        {
            await hubContext.Clients.All.SendAsync("StreamChanged", false);
            if (StreamEnded != null)
            {
                try
                {
                    await StreamEnded(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error firing StreamEnded");
                }
            }
        }

        public async Task OnCheer(ChannelCheer ev)
        {
            if (CheerEvent != null)
            {
                await CheerEvent(this, new CheerEventArgs
                {
                    Name = ev.UserLogin,
                    DisplayName = ev.UserName,
                    Amount = ev.Bits,
                    Message = ev.Message,
                    IsAnonymous = ev.IsAnonymous
                });
            }
        }

        public async Task OnFollow(ChannelFollow ev)
        {
            if (FollowEvent != null)
            {
                await FollowEvent(this, new FollowEventArgs
                {
                    Username = ev.UserLogin,
                    DisplayName = ev.UserName,
                    FollowDate = ev.FollowedAt.DateTime
                });
            }
        }

        public async Task OnIncomingRaid(RaidEventArgs args)
        {
            if (IncomingRaidEvent != null)
            {
                try
                {
                    await IncomingRaidEvent(this, args);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error In OnIncomingRaid");
                }
            }
        }

        //public async Task OnChatMessage(ChatMessageEventArgs message)
        //{
        //    await mediator.Publish(new ReceivedChatMessage { EventArgs = message });
        //}

        public async Task OnSubscription(SubscriptionEventArgs eventArgs)
        {
            if (SubscriptionEvent != null)
            {
                await SubscriptionEvent(this, eventArgs);
            }
        }

        public async Task OnSubscriptionGift(SubscriptionGiftEventArgs eventArgs)
        {
            if (SubscriptionGiftEvent != null)
            {
                await SubscriptionGiftEvent(this, eventArgs);
            }
        }

        public async Task OnSubscriptionEnd(string userName)
        {
            if (SubscriptionEndEvent != null)
            {
                await SubscriptionEndEvent(this, new SubscriptionEndEventArgs { Name = userName });
            }
        }
        public async Task OnAdBreakStartEvent(AdBreakStartEventArgs e)
        {
            if (AdBreakStartEvent != null)
            {
                await AdBreakStartEvent(this, e);
            }
        }

        public Task OnChannelPointRedeem(string userName, string title)
        {
            return OnChannelPointRedeem(userName, title, "");
        }

        public async Task OnChannelPointRedeem(string userName, string title, string userInput)
        {
            if (ChannelPointRedeemEvent != null)
            {
                await ChannelPointRedeemEvent(this, new ChannelPointRedeemEventArgs
                {
                    Sender = userName,
                    Title = title,
                    UserInput = userInput
                });
            }
        }

        public async Task OnUserJoined(string username)
        {
            if (UserJoinedEvent != null)
            {
                await UserJoinedEvent(this, new UserJoinedEventArgs { Username = username });
            }
        }

        public async Task OnUserLeft(string username)
        {
            if (UserLeftEvent != null)
            {
                await UserLeftEvent(this, new UserLeftEventArgs { Username = username });
            }
        }

        public async Task OnViewerBan(string username, bool unbanned)
        {
            if (BanEvent != null)
            {
                await BanEvent(this, new BanEventArgs { Name = username, IsUnBan = unbanned });
            }
        }
    }
}
