namespace DotNetTwitchBot.Bot.Commands
{
    public class CommandService
    {
        public event EventHandler<CommandEventArgs>? CommandEvent;
        public event EventHandler<String>? ChatMessage;

        public void ExecuteCommand()
        {
            if(CommandEvent != null)
            {
                var args = new CommandEventArgs();
                CommandEvent(this, args);
            }
        }
    }
}
