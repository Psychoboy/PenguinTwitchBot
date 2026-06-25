using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class PlaylistsRepository : GenericRepository<MusicPlaylist>, IPlaylistsRepository
    {
        public PlaylistsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
