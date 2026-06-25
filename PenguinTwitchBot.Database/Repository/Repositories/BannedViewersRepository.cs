using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class BannedViewersRepository : GenericRepository<BannedViewer>, IBannedViewersRepository
    {
        public BannedViewersRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
