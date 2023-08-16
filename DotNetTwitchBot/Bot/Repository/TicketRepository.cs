namespace DotNetTwitchBot.Bot.Repository
{
    public class TicketRepository : GenericRepository<ViewerTicket>, ITicketsRepository
    {
        public TicketRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
