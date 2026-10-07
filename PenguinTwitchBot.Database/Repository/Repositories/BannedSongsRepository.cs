namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class BannedSongsRepository(ApplicationDbContext context) : GenericRepository<BannedSong>(context), IBannedSongsRepository
    {
        public Task<BannedSong?> GetBySongIdAsync(string songId)
        {
            return _context.BannedSongs.AsNoTracking().FirstOrDefaultAsync(x => x.SongId == songId);
        }

        public Task<List<BannedSong>> GetAllOrderedAsync()
        {
            return _context.BannedSongs.AsNoTracking().OrderByDescending(x => x.BannedAt).ToListAsync();
        }
    }
}
