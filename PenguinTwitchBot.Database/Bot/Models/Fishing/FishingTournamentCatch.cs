using System.ComponentModel.DataAnnotations;

namespace PenguinTwitchBot.Database.Bot.Models.Fishing
{
    public class FishingTournamentCatch
    {
        [Key]
        public int Id { get; set; }

        public int FishingTournamentId { get; set; }
        public FishingTournament FishingTournament { get; set; } = null!;

        public int FishCatchId { get; set; }
        public FishCatch FishCatch { get; set; } = null!;
    }
}