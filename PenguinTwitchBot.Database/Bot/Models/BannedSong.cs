namespace PenguinTwitchBot.Database.Bot.Models
{
    public class BannedSong
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>YouTube video id (11 characters), always stored without the surrounding url.</summary>
        [Required]
        [MaxLength(64)]
        public string SongId { get; set; } = null!;

        [MaxLength(512)]
        public string Title { get; set; } = "";

        [MaxLength(512)]
        public string Reason { get; set; } = "";

        [MaxLength(128)]
        public string BannedBy { get; set; } = "";

        public DateTime BannedAt { get; set; } = DateTime.UtcNow;
    }
}
