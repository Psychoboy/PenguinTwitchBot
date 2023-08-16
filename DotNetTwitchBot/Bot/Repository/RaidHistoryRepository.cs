namespace DotNetTwitchBot.Bot.Repository
{
    public class RaidHistoryRepository : GenericRepository<RaidHistoryEntry>, IRaidHistoryRepository
    {
        public RaidHistoryRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
