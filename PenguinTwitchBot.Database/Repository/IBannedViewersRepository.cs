using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository
{
    public interface IBannedViewersRepository : IGenericRepository<BannedViewer>
    {
    }
}
