using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models
{
    public class ViewerTimeWithRank
    {
        public int? Id { get; set; }
        public string Username { get; set; } = "";
        public long Time { get; set; } = 0;
        public int Ranking { get; set; }
    }
}