using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DotNetTwitchBot.Bot.Models.Obs
{
    /// <summary>
    /// Represents an OBS WebSocket connection configuration
    /// </summary>
    [Table("obs_connections")]
    public class OBSConnection
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Url { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Password { get; set; } = string.Empty;

        public bool Enabled { get; set; } = true;

        public bool IsConnected { get; set; } = false;

        public DateTime? LastConnected { get; set; }

        public DateTime? LastDisconnected { get; set; }

        public string? LastError { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
