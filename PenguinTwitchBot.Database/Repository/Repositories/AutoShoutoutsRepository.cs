using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class AutoShoutoutsRepository : GenericRepository<AutoShoutout>, IAutoShoutoutsRepository
    {
        public AutoShoutoutsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
