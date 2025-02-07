namespace DotNetTwitchBot.Repository.Repositories
{
    public class FilteredQuotesRepository : GenericRepository<FilteredQuoteType>, IFilteredQuotesRepository
    {
        public FilteredQuotesRepository(ApplicationDbContext context) : base(context)
        {
        }

        public override Task BackupTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            return Task.CompletedTask;
        }
        public override Task RestoreTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            return Task.CompletedTask;
        }
    }
}
