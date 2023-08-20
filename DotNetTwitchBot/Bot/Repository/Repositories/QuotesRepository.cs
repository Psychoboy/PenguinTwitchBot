namespace DotNetTwitchBot.Bot.Repository.Repositories
{
    public class QuotesRepository : GenericRepository<QuoteType>, IQuotesRepository
    {
        public QuotesRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
