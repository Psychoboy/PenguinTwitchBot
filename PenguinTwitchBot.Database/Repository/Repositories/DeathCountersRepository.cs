using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class DeathCountersRepository : GenericRepository<DeathCounter>, IDeathCountersRepository
    {
        public DeathCountersRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
