using System.Reflection.Emit;
using DotNetTwitchBot.Bot.Events;

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

        public async Task OnCommand(TwitchLib.Client.Models.ChatCommand command)
        {
            if(CommandEvent != null) {
                var eventArgs = new CommandEventArgs();
                eventArgs.Arg = command.ArgumentsAsString;
                eventArgs.Args = command.ArgumentsAsList;
                eventArgs.Command = command.CommandText.ToLower();
                eventArgs.IsWhisper = false;
                eventArgs.Sender = command.ChatMessage.Username;
                eventArgs.isSub = command.ChatMessage.IsSubscriber;
                eventArgs.isMod = command.ChatMessage.IsBroadcaster || command.ChatMessage.IsModerator;
                eventArgs.isVip = command.ChatMessage.IsVip;
                if(eventArgs.Args.Count > 0) {
                    eventArgs.TargetUser = eventArgs.Args[0].Replace("@", "").Trim().ToLower();
                }
                await CommandEvent(this, eventArgs);
            }
        }

        public async Task SendChatMessage(string message) 
        {
            if(SendMessageEvent != null) {
                await SendMessageEvent(this, message);
            }
        }

        public async Task OnCheer(string sender) {
            if(CheerEvent != null) {
                await CheerEvent(this, new CheerEventArgs(){Sender = sender});
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
                    isVip = message.IsVip
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
    }
}
