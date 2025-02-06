using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models
{
    [Index(nameof(Username))]
    public class SubscriptionHistory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonIgnore]
        public int? Id { get; set; }
        public string Username { get; set; } = null!;
        public DateTime LastSub { get; set; } = DateTime.Now;
        public string UserId { get; set; } = string.Empty;
    }
}