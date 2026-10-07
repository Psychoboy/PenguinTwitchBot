using System.ComponentModel.DataAnnotations;

namespace PenguinTwitchBot.Database.Bot.Models.Fishing
{
    /// <summary>
    /// Snapshot of a catch that counted toward a tournament. Catch values are copied here so
    /// tournament history survives deletion of the source <see cref="FishCatch"/>.
    /// </summary>
    public class FishingTournamentCatch
    {
        [Key]
        public int Id { get; set; }

        public int FishingTournamentId { get; set; }
        public FishingTournament FishingTournament { get; set; } = null!;

        public int? FishCatchId { get; set; }
        public FishCatch? FishCatch { get; set; }

        [MaxLength(255)]
        public string UserId { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;
        public int FishTypeId { get; set; }
        public int Stars { get; set; } = 1;
        public double Weight { get; set; }
        public int GoldEarned { get; set; }
        public DateTime CaughtAt { get; set; } = DateTime.UtcNow;
    }
}