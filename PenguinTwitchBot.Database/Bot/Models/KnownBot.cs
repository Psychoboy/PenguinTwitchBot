using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PenguinTwitchBot.Database.Bot.Models
{
    public class KnownBot
    {
        [Key]
        public Guid Id { get; set; }
        public string Username { get; set; } = "";
    }
}