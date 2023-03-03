using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Events
{
    public class CheerEventArgs
    {
        public string? Sender {get;set;}
        public int Amount { get; internal set; }
        public string Message { get; internal set; } = "";
        public bool IsAnonymous { get; internal set; }
    }
}