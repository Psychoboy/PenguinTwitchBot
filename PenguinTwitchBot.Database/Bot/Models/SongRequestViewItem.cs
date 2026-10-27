using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PenguinTwitchBot.Database.Bot.Models
{
    [ExcludeFromBackup]
    public class SongRequestViewItem
    {
        [Key]
        public Guid Id { get; set; }
        public string SongId { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string RequestedBy { get; set; } = "";
        public TimeSpan Duration { get; set; }
    }
}