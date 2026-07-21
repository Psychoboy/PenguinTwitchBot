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
        Task<FishType?> GetFishTypeById(int id);
        Task AddFishType(FishType fishType);
        Task UpdateFishType(FishType fishType);
        Task DeleteFishType(int id);

        // Fish Catch Queries
        Task<List<FishCatch>> GetTopCatchesForFishType(int fishTypeId, int count = 10);
        Task<List<FishCatch>> GetUserCatches(string userId, int count = 50);
        Task<FishCatch?> GetUserBestCatchForFishType(string userId, int fishTypeId);
        Task<int> GetUserCatchCountForFishType(string userId, int fishTypeId);
        Task<Dictionary<int, FishCatch>> GetUserBestCatchesForAllFishTypes(string userId);
        Task<Dictionary<int, int>> GetUserCatchCountsForAllFishTypes(string userId);
        Task<List<FishingTournament>> GetAllFishingTournaments(int count = 100);
        Task<List<FishingTournament>> GetCurrentFishingTournaments();
        Task<List<FishingTournament>> GetPastFishingTournaments(int count = 25);
        Task<FishingTournament?> GetFishingTournamentById(int id);
        Task<List<FishingTournamentStanding>> GetFishingTournamentStandings(int tournamentId, int count = 10);
        Task<FishingTournament?> StartFishingTournament(int id);
        Task<FishingTournament?> CloneAndStartFishingTournament(int templateTournamentId);
        Task<FishingTournament?> ReopenFishingTournament(int id);
        Task<FishingTournament> SaveFishingTournament(FishingTournament tournament);
        Task<FishingTournament?> EndFishingTournament(int id);
        Task DeleteFishingTournament(int id);

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
