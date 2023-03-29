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
        private string? RawBroadcasterName { get; set; }
        public string? BotName { get; set; }

        public ServiceBackbone(ILogger<ServiceBackbone> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            RawBroadcasterName = configuration["broadcaster"];
            BotName = configuration["botName"];
        }
        public delegate Task AsyncEventHandler<TEventArgs>(object? sender, TEventArgs e);
        public delegate Task AsyncEventHandler<TEventArgs, TEventArgs2>(object? sender, TEventArgs e, TEventArgs2 e2);
        public event AsyncEventHandler<CommandEventArgs>? CommandEvent;
        public event AsyncEventHandler<String>? SendMessageEvent;
        public event AsyncEventHandler<String, String>? SendWhisperMessageEvent;
        public event AsyncEventHandler<CheerEventArgs>? CheerEvent;
        public event AsyncEventHandler<FollowEventArgs>? FollowEvent;
        public event AsyncEventHandler<SubscriptionEventArgs>? SubscriptionEvent;
        public event AsyncEventHandler<SubscriptionEventArgs>? SubscriptionEndEvent;
        public event AsyncEventHandler<ChatMessageEventArgs>? ChatMessageEvent;
        public event AsyncEventHandler<ChannelPointRedeemEventArgs>? ChannelPointRedeemEvent;
        public event AsyncEventHandler<UserJoinedEventArgs>? UserJoinedEvent;
        public event AsyncEventHandler<UserLeftEventArgs>? UserLeftEvent;

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
                    await CommandEvent(this, args);
                }
                catch (Exception e)
                {
                    _logger.LogCritical("Command Failure {0}", e);
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

        public async Task SendWhisperMessage(string name, string message)
        {
            if (SendWhisperMessageEvent != null)
            {
                await SendWhisperMessageEvent(this, name, message);
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
