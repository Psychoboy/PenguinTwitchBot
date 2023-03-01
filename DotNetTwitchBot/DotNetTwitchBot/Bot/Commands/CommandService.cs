using DotNetTwitchBot.Bot.Events;

namespace DotNetTwitchBot.Bot.Commands
{
    public class CommandService
    {
        //public event EventHandler<CommandEventArgs>? CommandEvent;
        public delegate Task AsyncEventHandler<TEventArgs>(object? sender, TEventArgs e);
        public event AsyncEventHandler<CommandEventArgs>? CommandEvent;
        public event AsyncEventHandler<String>? ChatMessage;
        public event AsyncEventHandler<CheerEventArgs>? CheerEvent;
        public event AsyncEventHandler<FollowEventArgs>? FollowEvent;
        public event AsyncEventHandler<SubscriptionEventArgs>? SubscriptionEvent;

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
                await CommandEvent(this, eventArgs);
            }
        }

        public async Task SendChatMessage(string message) 
        {
            if(ChatMessage != null) {
                await ChatMessage(this, message);
            }
        }

        public async Task OnCheer() {
            if(CheerEvent != null) {
                await CheerEvent(this, new CheerEventArgs());
            }
        }

        public async Task OnFollow() {
            if(FollowEvent != null) {
                await FollowEvent(this, new FollowEventArgs());
            }
        }
    }
}
