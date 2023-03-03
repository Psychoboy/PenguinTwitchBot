using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SQLite;

namespace DotNetTwitchBot.Bot.Models
{
    public class GiveawayEntry
    {
        [PrimaryKey, AutoIncrement]
        public int Id {get;set;}
        public string Username {get;set;} = "";
    }
}