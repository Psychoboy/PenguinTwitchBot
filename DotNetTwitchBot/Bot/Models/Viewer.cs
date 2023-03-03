using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SQLite;

namespace DotNetTwitchBot.Bot.Models
{
    public class Viewer
    {
        [PrimaryKey, AutoIncrement]
        public int Id {get;set;}
        [Indexed]
        public string Username {get;set;} = string.Empty;
        public string DisplayName {get;set;} = string.Empty;
        public DateTime LastSeen {get;set;}
        public bool isSub {get;set;}
        public bool isVip {get;set;}
        public bool isMod {get;set;}
        public bool isBroadcaster {get;set;}

    }
}