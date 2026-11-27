using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PenguinTwitchBot.Database.Bot.Models
{
    [IndexAttribute(nameof(Username))]
    public class SubscriptionHistory
    {
        [Key]
        public Guid Id { get; set; }
        public string Username { get; set; } = null!;
        public string DisplayName { get; set; } = string.Empty;
        public DateTime LastSub { get; set; } = DateTime.UtcNow;
        public string UserId { get; set; } = string.Empty;
        public bool IsActive { get; set; } = false;
        public string Tier { get; set; } = string.Empty;
        public bool IsGift { get; set; } = false;
        public string? GifterName { get; set; }
        public int TotalMonths { get; set; } = 0;
        public int ConsecutiveMonths { get; set; } = 0;
        public DateTime? EndedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}