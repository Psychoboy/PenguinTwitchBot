using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PenguinTwitchBot.Database.Bot.Core;

namespace PenguinTwitchBot.Database.Bot.Models.Credits
{
    [Table("CreditEvents")]
    [Index(nameof(OccurredAtUtc))]
    [Index(nameof(EventType), nameof(OccurredAtUtc))]
    [Index(nameof(StreamSessionId), nameof(EventType))]
    [Index(nameof(UserId), nameof(OccurredAtUtc))]
    public class CreditEvent
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(64)]
        public string EventType { get; set; } = string.Empty;

        [MaxLength(64)]
        public string UserId { get; set; } = string.Empty;

        [MaxLength(255)]
        public string Username { get; set; } = string.Empty;

        [MaxLength(255)]
        public string DisplayName { get; set; } = string.Empty;

        [MaxLength(512)]
        public string? AvatarUrl { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [MaxLength(32)]
        public string? Tier { get; set; }

        [MaxLength(512)]
        public string? Detail { get; set; }

        public string? MetadataJson { get; set; }

        public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

        public Guid? StreamSessionId { get; set; }

        public CreditStreamSession? StreamSession { get; set; }

        public bool IsAnonymous { get; set; }

        public bool IsManual { get; set; }

        public bool IsHidden { get; set; }

        public void NormalizeUsername()
        {
            Username = UsernameNormalizer.Normalize(Username);
        }
    }
}
