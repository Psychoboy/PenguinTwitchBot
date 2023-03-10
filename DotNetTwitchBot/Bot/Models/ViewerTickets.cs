using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using SQLite;

namespace DotNetTwitchBot.Bot.Models
{
    public class ViewerTickets
    {
        [PrimaryKey, AutoIncrement]
        public int? Id {get;set;}
        [Indexed]
        public string Username {get;set;} = "";
        public long Points {get;set;} = 0;
    }
}