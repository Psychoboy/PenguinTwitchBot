using System.ComponentModel.DataAnnotations.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models.Metrics
{
    public class SongRequestMetric
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string SongId { get; set; } = null!;
        public string Title { get; set; } = null!;
        public TimeSpan Duration { get; set; }
        public int RequestedCount { get; set; }
    }
}