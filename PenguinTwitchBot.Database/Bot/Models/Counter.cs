using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PenguinTwitchBot.Bot.Models
{
    [IndexAttribute(nameof(CounterName))]
    public class Counter
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonIgnore]
        public int? Id { get; set; }
        public string CounterName { get; set; } = String.Empty;
        public int Amount { get; set; }
    }
}