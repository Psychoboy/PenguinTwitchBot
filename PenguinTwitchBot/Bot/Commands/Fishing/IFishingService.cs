using PenguinTwitchBot.Database.Bot.Models.Fishing;

namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    /// <summary>
    /// Core fishing service for fish types, catches, gold, and settings management.
    /// Use specialized services for shop, inventory, gameplay, analytics, and leaderboards.
    /// </summary>
    public interface IFishingService
    {
        // Fish Type Management
        Task<List<FishType>> GetAllFishTypes();
        Task<List<FishType>> GetFishTypesWithCatches();
        Task<FishType?> GetFishTypeById(Guid id);
        Task AddFishType(FishType fishType);
        Task UpdateFishType(FishType fishType);
        Task DeleteFishType(Guid id);

        // Fish Catch Queries
        Task<List<FishCatch>> GetTopCatchesForFishType(Guid fishTypeId, int count = 10);
        Task<List<FishCatch>> GetUserCatches(string userId, int count = 50);
        Task<FishCatch?> GetUserBestCatchForFishType(string userId, Guid fishTypeId);
        Task<int> GetUserCatchCountForFishType(string userId, Guid fishTypeId);
        Task<Dictionary<Guid, FishCatch>> GetUserBestCatchesForAllFishTypes(string userId);
        Task<Dictionary<Guid, int>> GetUserCatchCountsForAllFishTypes(string userId);
        Task<List<FishingTournament>> GetAllFishingTournaments(int count = 100);
        Task<List<FishingTournament>> GetCurrentFishingTournaments();
        Task<List<FishingTournament>> GetPastFishingTournaments(int count = 25);
        Task<List<FishCatch>> GetRecentCatches(int count = 20);
        Task<FishingTournament?> GetFishingTournamentById(Guid id);
        Task<List<FishingTournamentStanding>> GetFishingTournamentStandings(Guid tournamentId, int count = 10);

        /// <summary>
        /// Returns the player currently holding (or that finished in) each enabled reward rule's placement,
        /// keyed by reward rule id.
        /// </summary>
        Task<Dictionary<Guid, FishingTournamentRewardStanding>> GetFishingTournamentRewardStandings(Guid tournamentId);
        Task<FishingTournament?> StartFishingTournament(Guid id);
        Task<FishingTournament?> CloneAndStartFishingTournament(Guid templateTournamentId);
        Task<FishingTournament?> ReopenFishingTournament(Guid id);
        Task<FishingTournament> SaveFishingTournament(FishingTournament tournament);
        Task<FishingTournament?> EndFishingTournament(Guid id);
        Task DeleteFishingTournament(Guid id);

        // Gold Management
        Task<FishingGold?> GetUserGold(string userId);
        Task AddGoldToUser(string userId, string username, int amount);
        Task RemoveGoldFromUser(string userId, int amount);
        Task SetUserGold(string userId, string username, int amount);
        Task<List<FishingGold>> GetAllPlayersWithGold();

        // Settings
        Task<FishingSettings?> GetSettings();
        Task UpdateSettings(FishingSettings settings);

        // Admin Operations
        Task ResetAllUserData();
        Task<int> SyncAllFishRarities();
        Task<int> CleanOrphanedTournamentCategories();
    }
}
