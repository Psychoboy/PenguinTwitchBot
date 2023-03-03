using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SQLite;

namespace DotNetTwitchBot.Bot.Models
{
    public class Follower
    {
        [PrimaryKey, AutoIncrement]
        public int? Id {get;set;}
        [Indexed]
        public string Username {get;set;} = string.Empty;
        public string DisplayName {get;set;} = string.Empty;
        public DateTime FollowDate {get;set;} = DateTime.MinValue;
    }
}