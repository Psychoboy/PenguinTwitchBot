using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models.Giveaway
{
    [Index(nameof(Username), IsUnique = true)]
    public class GiveawayEntry
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Int64? Id { get; set; }
        public string Username { get; set; } = "";
        public Int32 Tickets { get; set; } = 0;
    }
}