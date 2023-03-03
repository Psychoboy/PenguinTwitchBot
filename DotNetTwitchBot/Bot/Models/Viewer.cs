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
        public int? Id {get;set;}
        [Indexed]
        public string Username {get;set;} = string.Empty;
        public string DisplayName {get;set;} = string.Empty;
        public DateTime LastSeen {get;set;} = DateTime.MinValue;
        public bool isSub {get;set;} = false;
        public bool isVip {get;set;} = false;
        public bool isMod {get;set;} = false;
        public bool isBroadcaster {get;set;} = false;

    }
}