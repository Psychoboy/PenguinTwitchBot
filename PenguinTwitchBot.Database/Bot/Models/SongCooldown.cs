using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Bot.Models
{
    [Index(nameof(SongId), IsUnique = true)]
    public class SongCooldown
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>YouTube video id (11 characters), stored without surrounding URL.</summary>
        [Required]
        [MaxLength(64)]
        public string SongId { get; set; } = null!;

        [MaxLength(512)]
        public string Title { get; set; } = "";

        public DateTime CooldownExpiresAt { get; set; } = DateTime.UtcNow;

        [MaxLength(128)]
        public string AddedBy { get; set; } = "";

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}
