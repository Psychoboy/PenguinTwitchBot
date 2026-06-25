using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class PlaylistsRepository : GenericRepository<MusicPlaylist>, IPlaylistsRepository
    {
        public PlaylistsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
