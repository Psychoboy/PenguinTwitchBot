namespace PenguinTwitchBot.Repository.Repositories
{
    public class WordFiltersRepository : GenericRepository<WordFilter>, IWordFiltersRepository
    {
        public WordFiltersRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
