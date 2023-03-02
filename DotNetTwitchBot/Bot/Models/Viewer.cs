using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models
{
    public class Viewer
    {
        public int Id {get;set;}
        public string Username {get;set;}
        public string DisplayName {get;set;}

        public DateTime LastSeen {get;set;}
        public bool isSub {get;set;}
        public bool isVip {get;set;}
        public bool isMod {get;set;}

    }
}