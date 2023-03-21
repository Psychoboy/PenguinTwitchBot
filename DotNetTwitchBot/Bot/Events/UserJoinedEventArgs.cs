using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Events
{
    public class UserJoinedEventArgs
    {
        public string Username { get; set; } = string.Empty;
    }
}