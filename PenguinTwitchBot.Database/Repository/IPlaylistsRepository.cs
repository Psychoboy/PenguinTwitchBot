using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository
{
    public interface IPlaylistsRepository : IGenericRepository<MusicPlaylist>
    {
    }
}
