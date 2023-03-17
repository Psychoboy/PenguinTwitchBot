using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models
{
    public class BaseCommandProperties
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int? Id { get; set; }
        public string CommandName { get; set; } = null!;
        public int UserCooldown { get; set; } = 5;
        public int GlobalCooldown { get; set; } = 5;
        public Rank MinimumRank { get; set; } = Rank.Viewer;
        public int Cost { get; set; } = 0;
        public bool Disabled { get; set; } = false;

    }
}