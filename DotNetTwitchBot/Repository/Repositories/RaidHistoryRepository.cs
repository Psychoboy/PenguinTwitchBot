namespace DotNetTwitchBot.Repository.Repositories
{
    public class RaidHistoryRepository : GenericRepository<RaidHistoryEntry>, IRaidHistoryRepository
    {
        public RaidHistoryRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
