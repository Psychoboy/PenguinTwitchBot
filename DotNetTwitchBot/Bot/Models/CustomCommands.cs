using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models
{
    [Index(nameof(CommandName))]
    public class CustomCommands
    {
        public enum Rank
        {
            Viewer,
            Follower,
            Subscriber,
            Moderator,
            Streamer
        }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int? Id { get; set; }
        public string CommandName { get; set; } = String.Empty;
        public string Response { get; set; } = String.Empty;
        public int UserCooldown { get; set; } = 5;
        public int GlobalCooldown { get; set; } = 5;
        public Rank MinimumRank { get; set; } = Rank.Viewer;

    }
}