namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class FilteredQuotesRepository : GenericRepository<FilteredQuoteType>, IFilteredQuotesRepository
    {
        public FilteredQuotesRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
