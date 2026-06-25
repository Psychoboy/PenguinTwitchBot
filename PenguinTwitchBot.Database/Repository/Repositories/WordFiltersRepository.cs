using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class WordFiltersRepository : GenericRepository<WordFilter>, IWordFiltersRepository
    {
        public WordFiltersRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
