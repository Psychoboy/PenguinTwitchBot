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
        private string? BroadcasterName { get; set; }
        private string? BotName { get; set; }

        public ServiceBackbone(ILogger<ServiceBackbone> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            BroadcasterName = configuration["broadcaster"];
            BotName = configuration["botName"];
        }
        public delegate Task AsyncEventHandler<TEventArgs>(object? sender, TEventArgs e);
        public event AsyncEventHandler<CommandEventArgs>? CommandEvent;
        public event AsyncEventHandler<WhisperEventArgs>? WhisperEvent;
        public event AsyncEventHandler<String>? SendMessageEvent;
        public event AsyncEventHandler<CheerEventArgs>? CheerEvent;
        public event AsyncEventHandler<FollowEventArgs>? FollowEvent;
        public event AsyncEventHandler<SubscriptionEventArgs>? SubscriptionEvent;
        public event AsyncEventHandler<SubscriptionEventArgs>? SubscriptionEndEvent;
        public event AsyncEventHandler<ChatMessageEventArgs>? ChatMessageEvent;
        public event AsyncEventHandler<ChannelPointRedeemEventArgs>? ChannelPointRedeemEvent;
        public event AsyncEventHandler<UserJoinedEventArgs>? UserJoinedEvent;
        public event AsyncEventHandler<UserLeftEventArgs>? UserLeftEvent;

        public bool IsOnline { get; set; } = false;
        public bool IsBroadcasterOrBot(string name)
        {
            return (name.Equals(BroadcasterName, StringComparison.CurrentCultureIgnoreCase) ||
                    name.Equals(BotName, StringComparison.CurrentCultureIgnoreCase));
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
                    await CommandEvent(this, eventArgs);
                }
                catch (Exception e)
                {
                    _logger.LogCritical("Command Failure {0}", e);
                }
            }
        }

        public async Task OnWhisperCommand(WhisperCommand command)
        {
            if (WhisperEvent != null)
            {
                var eventArgs = new WhisperEventArgs()
                {
                    Arg = command.ArgumentsAsString,
                    Args = command.ArgumentsAsList,
                    Command = command.CommandText.ToLower(),
                    IsWhisper = true,
                    Sender = command.WhisperMessage.Username
                };
                try
                {
                    await WhisperEvent(this, eventArgs);
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

        // public async Task OnCheer(string sender) {
        //     if(CheerEvent != null) {
        //         await CheerEvent(this, new CheerEventArgs(){
        //             Sender = sender
        //             });
        //     }
        // }

        public async Task OnCheer(ChannelCheer ev)
        {
            if (CheerEvent != null)
            {
                await CheerEvent(this, new CheerEventArgs()
                {
                    Sender = ev.UserName,
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

        public async Task OnSubscription(string sender)
        {
            if (SubscriptionEvent != null)
            {
                await SubscriptionEvent(this, new SubscriptionEventArgs() { Sender = sender });
            }
        }

        public async Task OnSubscriptionEnd(string userName)
        {
            if (SubscriptionEndEvent != null)
            {
                await SubscriptionEndEvent(this, new SubscriptionEventArgs() { Sender = userName });
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
