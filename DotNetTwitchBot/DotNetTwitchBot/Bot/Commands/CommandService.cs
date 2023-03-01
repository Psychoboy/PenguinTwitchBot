namespace DotNetTwitchBot.Bot.Commands
{
    public class CommandService
    {
        public event EventHandler<CommandEventArgs>? CommandEvent;
        public event EventHandler<String>? ChatMessage;

        public void ExecuteCommand(TwitchLib.Client.Models.ChatCommand command)
        {
            if(CommandEvent != null)
            {
                var eventArgs = new CommandEventArgs();
                eventArgs.Arg = command.ArgumentsAsString;
                eventArgs.Args = command.ArgumentsAsList;
                eventArgs.Command = command.CommandText.ToLower();
                eventArgs.IsWhisper = false;
                eventArgs.Sender = command.ChatMessage.Username;
                eventArgs.isSub = command.ChatMessage.IsSubscriber;
                eventArgs.isMod = command.ChatMessage.IsBroadcaster || command.ChatMessage.IsModerator;
                eventArgs.isVip = command.ChatMessage.IsVip;
                CommandEvent(this, eventArgs);
            }
        }

        public void SendChatMessage(string message) 
        {
            if(ChatMessage != null) {
                ChatMessage(this, message);
            }
        }
    }
}
