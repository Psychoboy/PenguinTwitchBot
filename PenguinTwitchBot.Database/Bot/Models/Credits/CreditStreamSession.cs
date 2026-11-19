using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PenguinTwitchBot.Database.Bot.Models.Credits
{
    [Table("CreditStreamSessions")]
    [Index(nameof(StartedAtUtc))]
    public class CreditStreamSession
    {
        [Key]
        public Guid Id { get; set; }

        public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? EndedAtUtc { get; set; }

        [MaxLength(255)]
        public string? Title { get; set; }

        [MaxLength(255)]
        public string? GameName { get; set; }

        public bool IsManualCheckpoint { get; set; }
    }
}
