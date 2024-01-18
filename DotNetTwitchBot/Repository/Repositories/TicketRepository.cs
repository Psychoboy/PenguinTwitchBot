namespace DotNetTwitchBot.Repository.Repositories
{
    public class TicketRepository : GenericRepository<ViewerTicket>, ITicketsRepository
    {
        public TicketRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
