using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models
{
    public class ViewerMessageCountWithRank
    {
        public int? Id { get; set; }
        public string Username { get; set; } = "";
        public long MessageCount { get; set; } = 0;
        public int Ranking { get; set; }
    }
}