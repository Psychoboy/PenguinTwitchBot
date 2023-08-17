namespace DotNetTwitchBot.Bot.Repository.Repositories
{
    public class TicketsWithRankRepository : GenericRepository<ViewerTicketWithRanks>, ITicketsWithRankRepository
    {
        public TicketsWithRankRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
