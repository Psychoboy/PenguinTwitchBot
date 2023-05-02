using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models.Giveaway
{
    public class GiveawayWinner
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Int64? Id { get; set; }
        public string Username { get; set; } = "";
        public DateTime WinningDate { get; set; } = DateTime.Now;
        public string Prize { get; set; } = "";
    }
}