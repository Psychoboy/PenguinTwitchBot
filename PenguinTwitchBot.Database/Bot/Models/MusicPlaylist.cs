using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PenguinTwitchBot.Database.Bot.Models
{
    public class MusicPlaylist
    {
        [Key]
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public List<Song> Songs { get; set; } = new List<Song>();

    }
}