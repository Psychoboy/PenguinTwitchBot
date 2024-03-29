using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models
{
    public class SongRequestViewItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int? Id { get; set; }
        public string SongId { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string RequestedBy { get; set; } = "DJ Waffle";
        public TimeSpan Duration { get; set; }
    }
}