namespace DotNetTwitchBot.Bot.Repository
{
    public class TicketsWithRankRepository : GenericRepository<ViewerTicketWithRanks>, ITicketsWithRankRepository
    {
        public TicketsWithRankRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
