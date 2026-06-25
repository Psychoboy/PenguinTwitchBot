using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class SettingsRepository : GenericRepository<Setting>, ISettingsRepository
    {
        public SettingsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
