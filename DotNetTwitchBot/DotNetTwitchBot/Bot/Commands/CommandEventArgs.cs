namespace DotNetTwitchBot.Bot.Commands
{
    public sealed class CommandEventArgs{
        public string Command { get; set; } = "";
        public string Arg { get; set; } = "";
        public List<string> Args { get; set; } = new List<string>();
        public string Sender { get; set; } = "";
        public bool IsWhisper { get; set; } = false;

    }
}
