using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Reflection.Emit;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Events;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.Client.Models;
using DotNetTwitchBot.Bot.Commands.Moderation;
using DotNetTwitchBot.Bot.Commands;

namespace DotNetTwitchBot.Bot.Core
{
    public class ServiceBackbone
    {
        private ILogger<ServiceBackbone> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IKnownBots _knownBots;
        private readonly CommandHandler _commandHandler;
        static SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1);
        private string? RawBroadcasterName { get; set; }
        public string? BotName { get; set; }

        public ServiceBackbone(
            ILogger<ServiceBackbone> logger,
            IKnownBots knownBots,
            IConfiguration configuration,
            IServiceScopeFactory scopeFactory,
            CommandHandler commandHandler)
        {
            _logger = logger;
            RawBroadcasterName = configuration["broadcaster"];
            BotName = configuration["botName"];
            _scopeFactory = scopeFactory;
            _knownBots = knownBots;
            _commandHandler = commandHandler;
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
            return _knownBots.IsStreamerOrBot(name);
        }

        public bool IsKnownBot(string name)
        {
            return _knownBots.IsKnownBot(name);
        }

        public bool IsKnownBotOrCurrentStreamer(string name)
        {
            return _knownBots.IsKnownBotOrCurrentStreamer(name);
        }

        public async Task RunCommand(CommandEventArgs args)
        {
            // if (CommandEvent != null)
            // {
            //     try
            //     {
            //         await _semaphoreSlim.WaitAsync();
            //         await CommandEvent(this, args);
            //     }
            //     catch (Exception e)
            //     {
            //         _logger.LogCritical("Command Failure {0}", e);
            //     }
            //     finally
            //     {
            //         _semaphoreSlim.Release();
            //     }
            // }
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
                    //await CommandEvent(this, eventArgs);
                    var commandService = _commandHandler.GetCommand(eventArgs.Command);
                    if (commandService == null)
                    {
                        throw new Exception($"Command service not found {eventArgs.Command}");
                    }
                    if (CheckPermission(commandService.CommandProperties, eventArgs))
                    {
                        await commandService.CommandService.OnCommand(this, eventArgs);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogWarning("Command Failure {0}", e);
                }
                finally
                {
                    _semaphoreSlim.Release();
                }
            }
        }

        private bool CheckPermission(BaseCommandProperties commandProperties, CommandEventArgs eventArgs)
        {
            switch (commandProperties.MinimumRank)
            {
                case Rank.Viewer:
                case Rank.Regular:
                    return true;
                case Rank.Follower:
                    return true; //Need to add this check
                case Rank.Subscriber:
                    return eventArgs.IsSubOrHigher();
                case Rank.Vip:
                    return eventArgs.IsVipOrHigher();
                case Rank.Moderator:
                    return eventArgs.IsModOrHigher();
                case Rank.Streamer:
                    return IsBroadcasterOrBot(eventArgs.Name);
                default:
                    return false;
            }
        }

        private List<string> AllowedWhisperCommands
        {
            get
            {
                return new List<string>
                {
                    "entries"
                };
            }
        }

        public async Task OnWhisperCommand(CommandEventArgs command)
        {
            if (CommandEvent != null)
            {
                if (IsBroadcasterOrBot(command.Name) || AllowedWhisperCommands.Contains(command.Command))
                {
                    command.isBroadcaster = IsBroadcasterOrBot(command.Name);
                    try
                    {
                        await CommandEvent(this, command);
                    }
                    catch (Exception e)
                    {
                        _logger.LogCritical("Whisper Failure {0}", e);
                    }
                }
                // if (!IsBroadcasterOrBot(command.WhisperMessage.Username)) { return; }
                // var eventArgs = new CommandEventArgs()
                // {
                //     Arg = command.ArgumentsAsString,
                //     Args = command.ArgumentsAsList,
                //     Command = command.CommandText.ToLower(),
                //     IsWhisper = true,
                //     Name = command.WhisperMessage.Username,
                //     DisplayName = command.WhisperMessage.DisplayName,
                //     isSub = true,
                //     isMod = true,
                //     isBroadcaster = true,
                //     TargetUser = command.ArgumentsAsList.Count > 0
                //         ? command.ArgumentsAsList[0].Replace("@", "").Trim().ToLower()
                //         : ""
                // };
                // try
                // {
                //     await CommandEvent(this, command);
                // }
                // catch (Exception e)
                // {
                //     _logger.LogCritical("Whisper Failure {0}", e);
                // }
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
                    Name = message.Username.ToLower(),
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
