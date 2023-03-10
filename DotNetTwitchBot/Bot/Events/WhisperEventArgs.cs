using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Events
{
    public class WhisperEventArgs
    {
        public string Command { get; set; } = "";
        public string Arg { get; set; } = "";
        public List<string> Args { get; set; } = new List<string>();
        public string Sender { get; set; } = "";
        public bool IsWhisper { get; set; } = true;
    }
}