using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;

namespace DotNetTwitchBot.Bot.Models
{
    public class ViewerPoints
    {
        public int Id {get;set;}
        public string Username {get;set;} = "";
        public string DisplayName {get;set;} = "";
        public int Points {get;set;} = 0;
    }
}