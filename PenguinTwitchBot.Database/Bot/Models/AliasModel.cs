using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PenguinTwitchBot.Database.Bot.Models
{
    public class AliasModel
    {
        [Key]
        public Guid Id { get; set; }
        public string AliasName { get; set; } = null!;
        public string CommandName { get; set; } = null!;
    }
}