using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PenguinTwitchBot.Database.Bot.Models
{
    [Index(nameof(SessionKey), IsUnique = true)]
    public class OpenAiResponseCode
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string SessionKey { get; set; } = string.Empty;
        public string PreviousResponseId { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}

