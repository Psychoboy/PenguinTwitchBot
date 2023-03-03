using System.Reflection.Emit;
using DotNetTwitchBot.Bot.Events;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;

namespace DotNetTwitchBot.Bot.Core
{
    public class EventService
    {
        public delegate Task AsyncEventHandler<TEventArgs>(object? sender, TEventArgs e);
        public event AsyncEventHandler<CommandEventArgs>? CommandEvent;
        public event AsyncEventHandler<String>? SendMessageEvent;
        public event AsyncEventHandler<CheerEventArgs>? CheerEvent;
        public event AsyncEventHandler<FollowEventArgs>? FollowEvent;
        public event AsyncEventHandler<SubscriptionEventArgs>? SubscriptionEvent;
        public event AsyncEventHandler<ChatMessageEventArgs>? ChatMessageEvent;
        public event AsyncEventHandler<ChannelPointRedeemEventArgs>? ChannelPointRedeemEvent;
        public event AsyncEventHandler<UserJoinedEventArgs>? UserJoinedEvent;
        public event AsyncEventHandler<UserLeftEventArgs>? UserLeftEvent;

        public bool IsOnline {get;set;} = false;

        public async Task OnCommand(TwitchLib.Client.Models.ChatCommand command)
        {
            if(CommandEvent != null) {
                var eventArgs = new CommandEventArgs(){
                    Arg = command.ArgumentsAsString,
                    Args = command.ArgumentsAsList,
                    Command = command.CommandText.ToLower(),
                    IsWhisper = false,
                    Sender = command.ChatMessage.Username,
                    isSub = command.ChatMessage.IsSubscriber,
                    isMod = command.ChatMessage.IsBroadcaster || command.ChatMessage.IsModerator,
                    isVip = command.ChatMessage.IsVip,
                    isBroadcaster = command.ChatMessage.IsBroadcaster,
                    TargetUser = command.ArgumentsAsList.Count > 0 
                        ? command.ArgumentsAsList[0].Replace("@", "").Trim().ToLower() 
                        : ""
                };
                await CommandEvent(this, eventArgs);
            }
        }

        public async Task SendChatMessage(string message) 
        {
            if(SendMessageEvent != null) {
                await SendMessageEvent(this, message);
            }
        }

        // public async Task OnCheer(string sender) {
        //     if(CheerEvent != null) {
        //         await CheerEvent(this, new CheerEventArgs(){
        //             Sender = sender
        //             });
        //     }
        // }

        internal async Task OnCheer(ChannelCheer ev)
        {
            if(CheerEvent != null) {
                await CheerEvent(this, new CheerEventArgs(){
                    Sender = ev.UserName,
                    Amount = ev.Bits,
                    Message = ev.Message,
                    IsAnonymous = ev.IsAnonymous
                });
            }
        }

        public async Task OnFollow(string sender) {
            if(FollowEvent != null) {
                await FollowEvent(this, new FollowEventArgs(){Sender = sender});
            }
        }

        public async Task OnChatMessage(TwitchLib.Client.Models.ChatMessage message) {
            if(ChatMessageEvent != null) {
                await ChatMessageEvent(this, new ChatMessageEventArgs(){
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

        public async Task OnSubscription(string sender) {
            if(SubscriptionEvent != null) {
                await SubscriptionEvent(this, new SubscriptionEventArgs(){Sender = sender});
            }
        }

        public async Task OnChannelPointRedeem(string userName, string title, string userInput)
        {
            if(ChannelPointRedeemEvent != null) {
                await ChannelPointRedeemEvent(this, new ChannelPointRedeemEventArgs(){
                    Sender = userName,
                    Title = title,
                    UserInput = userInput
                });
            }
        }

        public async Task OnUserJoined(string username) {
            if(UserJoinedEvent != null) {
                await UserJoinedEvent(this, new UserJoinedEventArgs(){Username = username});
            }
        }

        public async Task OnUserLeft(string username) {
            if(UserLeftEvent != null) {
                await UserLeftEvent(this, new UserLeftEventArgs(){Username = username});
            }
        }

        
    }
}
