using System.Runtime.ExceptionServices;
using System.Reflection.Emit;
using DotNetTwitchBot.Bot.Events;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.Client.Models;

namespace DotNetTwitchBot.Bot.Core
{
    public class ServiceBackbone
    {
        private ILogger<ServiceBackbone> _logger;
        private IConfiguration _configuration;
        private readonly IServiceScopeFactory _scopeFactory;
        static SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1);
        private string? RawBroadcasterName { get; set; }
        public string? BotName { get; set; }

        public ServiceBackbone(ILogger<ServiceBackbone> logger, IConfiguration configuration, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _configuration = configuration;
            RawBroadcasterName = configuration["broadcaster"];
            BotName = configuration["botName"];
            _scopeFactory = scopeFactory;
        }

        public delegate Task AsyncEventHandler(object? sender);
        public delegate Task AsyncEventHandler<TEventArgs>(object? sender, TEventArgs e);
        public delegate Task AsyncEventHandler<TEventArgs, TEventArgs2>(object? sender, TEventArgs e, TEventArgs2 e2);
        public event AsyncEventHandler<CommandEventArgs>? CommandEvent;
        public event AsyncEventHandler<String>? SendMessageEvent;
        public event AsyncEventHandler<String, String>? SendWhisperMessageEvent;
        public event AsyncEventHandler<CheerEventArgs>? CheerEvent;
        public event AsyncEventHandler<FollowEventArgs>? FollowEvent;
        public event AsyncEventHandler<SubscriptionEventArgs>? SubscriptionEvent;
        public event AsyncEventHandler<SubscriptionGiftEventArgs>? SubscriptionGiftEvent;
        public event AsyncEventHandler<SubscriptionEndEventArgs>? SubscriptionEndEvent;
        public event AsyncEventHandler<ChatMessageEventArgs>? ChatMessageEvent;
        public event AsyncEventHandler<ChannelPointRedeemEventArgs>? ChannelPointRedeemEvent;
        public event AsyncEventHandler<UserJoinedEventArgs>? UserJoinedEvent;
        public event AsyncEventHandler<UserLeftEventArgs>? UserLeftEvent;
        public event AsyncEventHandler<RaidEventArgs>? IncomingRaidEvent;
        public event AsyncEventHandler? StreamStarted;
        public event AsyncEventHandler? StreamEnded;

        public bool IsOnline { get; set; } = false;
        public string BroadcasterName { get { return RawBroadcasterName != null ? RawBroadcasterName : ""; } }
        public bool IsBroadcasterOrBot(string name)
        {
            return (name.Equals(RawBroadcasterName, StringComparison.CurrentCultureIgnoreCase) ||
                    name.Equals(BotName, StringComparison.CurrentCultureIgnoreCase));
        }

        public async Task RunCommand(CommandEventArgs args)
        {
            if (CommandEvent != null)
            {
                try
                {
                    await _semaphoreSlim.WaitAsync();
                    await CommandEvent(this, args);
                }
                catch (Exception e)
                {
                    _logger.LogCritical("Command Failure {0}", e);
                }
                finally
                {
                    _semaphoreSlim.Release();
                }
            }
        }

        public async Task OnCommand(TwitchLib.Client.Models.ChatCommand command)
        {
            if (CommandEvent != null)
            {
                var eventArgs = new CommandEventArgs()
                {
                    Arg = command.ArgumentsAsString,
                    Args = command.ArgumentsAsList,
                    Command = command.CommandText.ToLower(),
                    IsWhisper = false,
                    Name = command.ChatMessage.Username,
                    DisplayName = command.ChatMessage.DisplayName,
                    isSub = command.ChatMessage.IsSubscriber,
                    isMod = command.ChatMessage.IsBroadcaster || command.ChatMessage.IsModerator,
                    isVip = command.ChatMessage.IsVip,
                    isBroadcaster = command.ChatMessage.IsBroadcaster,
                    TargetUser = command.ArgumentsAsList.Count > 0
                        ? command.ArgumentsAsList[0].Replace("@", "").Trim().ToLower()
                        : ""
                };
                try
                {
                    await _semaphoreSlim.WaitAsync();
                    await CommandEvent(this, eventArgs);
                }
                catch (Exception e)
                {
                    _logger.LogCritical("Command Failure {0}", e);
                }
                finally
                {
                    _semaphoreSlim.Release();
                }
            }
        }

        public async Task OnWhisperCommand(WhisperCommand command)
        {
            if (CommandEvent != null)
            {
                if (!IsBroadcasterOrBot(command.WhisperMessage.Username)) { return; }
                var eventArgs = new CommandEventArgs()
                {
                    Arg = command.ArgumentsAsString,
                    Args = command.ArgumentsAsList,
                    Command = command.CommandText.ToLower(),
                    IsWhisper = true,
                    Name = command.WhisperMessage.Username,
                    DisplayName = command.WhisperMessage.DisplayName,
                    isSub = true,
                    isMod = true,
                    isBroadcaster = true,
                    TargetUser = command.ArgumentsAsList.Count > 0
                        ? command.ArgumentsAsList[0].Replace("@", "").Trim().ToLower()
                        : ""
                };
                try
                {
                    await CommandEvent(this, eventArgs);
                }
                catch (Exception e)
                {
                    _logger.LogCritical("Whisper Failure {0}", e);
                }
            }
        }

        public async Task SendChatMessage(string message)
        {
            if (SendMessageEvent != null)
            {
                await SendMessageEvent(this, message);
            }
        }

        public async Task SendChatMessage(string name, string message)
        {
            if (SendMessageEvent != null)
            {
                await SendMessageEvent(this, string.Format("@{0}, {1}", name, message));
            }
        }
        public async Task SendChatMessageWithTitle(string viewerName, string message)
        {
            if (SendMessageEvent != null)
            {
                using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var viewerService = scope.ServiceProvider.GetRequiredService<Commands.Features.ViewerFeature>();
                    var nameWithTitle = await viewerService.GetNameWithTitle(viewerName);
                    await SendMessageEvent(this, string.Format("{0}, {1}", string.IsNullOrWhiteSpace(nameWithTitle) ? viewerName : nameWithTitle, message));
                }
            }
        }

        public async Task SendWhisperMessage(string name, string message)
        {
            if (SendWhisperMessageEvent != null)
            {
                await SendWhisperMessageEvent(this, name, message);
            }
        }

        public async Task OnStreamStarted()
        {
            if (StreamStarted != null)
            {
                try
                {
                    await StreamStarted(this);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error firing StreamStarted");
                }
            }
        }

        public async Task OnStreamEnded()
        {
            if (StreamEnded != null)
            {
                try
                {
                    await StreamEnded(this);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error firing StreamEnded");
                }
            }
        }

        public async Task OnCheer(ChannelCheer ev)
        {
            if (CheerEvent != null)
            {
                await CheerEvent(this, new CheerEventArgs()
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
                await FollowEvent(this, new FollowEventArgs()
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
                    _logger.LogError(ex, "Error In OnIncomingRaid");
                }
            }
        }

        public async Task OnChatMessage(TwitchLib.Client.Models.ChatMessage message)
        {
            if (ChatMessageEvent != null)
            {
                await ChatMessageEvent(this, new ChatMessageEventArgs()
                {
                    Message = message.Message,
                    Sender = message.Username.ToLower(),
                    DisplayName = message.DisplayName,
                    isSub = message.IsSubscriber,
                    isMod = message.IsModerator,
                    isVip = message.IsVip,
                    isBroadcaster = message.IsBroadcaster
                });
            }
        }

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
                await SubscriptionEndEvent(this, new SubscriptionEndEventArgs() { Name = userName });
            }
        }

        public async Task OnChannelPointRedeem(string userName, string title, string userInput)
        {
            if (ChannelPointRedeemEvent != null)
            {
                await ChannelPointRedeemEvent(this, new ChannelPointRedeemEventArgs()
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
                await UserJoinedEvent(this, new UserJoinedEventArgs() { Username = username });
            }
        }

        public async Task OnUserLeft(string username)
        {
            if (UserLeftEvent != null)
            {
                await UserLeftEvent(this, new UserLeftEventArgs() { Username = username });
            }
        }
    }
}
