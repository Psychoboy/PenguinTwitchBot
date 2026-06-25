using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository
{
    public interface IPlaylistsRepository : IGenericRepository<MusicPlaylist>
    {
    }
}
