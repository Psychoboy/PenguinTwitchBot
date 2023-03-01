using System.ComponentModel;

namespace DotNetTwitchBot.Bot.Events
{
    public sealed class CommandEventArgs{
        public string Command { get; set; } = "";
        public string Arg { get; set; } = "";
        public List<string> Args { get; set; } = new List<string>();
        public string Sender { get; set; } = "";
        public bool IsWhisper { get; set; } = false;
        public bool isSub { get; set; }
        public bool isMod { get; set; }
        public bool isVip { get; set; }
    }
}
