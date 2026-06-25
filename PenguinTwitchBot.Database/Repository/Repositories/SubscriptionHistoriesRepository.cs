using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class SubscriptionHistoriesRepository : GenericRepository<SubscriptionHistory>, ISubscriptionHistoriesRepository
    {
        public SubscriptionHistoriesRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
