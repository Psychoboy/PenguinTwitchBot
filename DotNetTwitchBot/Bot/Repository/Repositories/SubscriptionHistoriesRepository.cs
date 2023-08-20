namespace DotNetTwitchBot.Bot.Repository.Repositories
{
    public class SubscriptionHistoriesRepository : GenericRepository<SubscriptionHistory>, ISubscriptionHistoriesRepository
    {
        public SubscriptionHistoriesRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
