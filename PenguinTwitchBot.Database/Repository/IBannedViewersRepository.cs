using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository
{
    public interface IBannedViewersRepository : IGenericRepository<BannedViewer>
    {
    }
}
