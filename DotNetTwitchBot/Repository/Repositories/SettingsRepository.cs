namespace DotNetTwitchBot.Repository.Repositories
{
    public class SettingsRepository : GenericRepository<Setting>, ISettingsRepository
    {
        public SettingsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
