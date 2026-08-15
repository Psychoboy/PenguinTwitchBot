namespace PenguinTwitchBot.Database.Repository
{
    public interface IBannedSongsRepository : IGenericRepository<BannedSong>
    {
        Task<BannedSong?> GetBySongIdAsync(string songId);
        Task<List<BannedSong>> GetAllOrderedAsync();
    }
}
