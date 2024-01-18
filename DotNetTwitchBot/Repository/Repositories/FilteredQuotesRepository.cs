namespace DotNetTwitchBot.Repository.Repositories
{
    public class FilteredQuotesRepository : GenericRepository<FilteredQuoteType>, IFilteredQuotesRepository
    {
        public FilteredQuotesRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
