using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PenguinTwitchBot.Database.Bot.Models
{
    [IndexAttribute(nameof(CounterName), IsUnique = true)]
    public class Counter
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonIgnore]
        public int? Id { get; set; }
        public string CounterName { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public int Amount { get; set; }
        public int InitialValue { get; set; } = 0;
        public int Step { get; set; } = 1;
        public int? Min { get; set; }
        public int? Max { get; set; }

        public Rank IncrementRank { get; set; } = Rank.Moderator;
        public Rank DecrementRank { get; set; } = Rank.Moderator;
        public Rank ResetRank { get; set; } = Rank.Streamer;
        public Rank SetRank { get; set; } = Rank.Moderator;
    }
}