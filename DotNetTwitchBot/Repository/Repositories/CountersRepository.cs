namespace DotNetTwitchBot.Repository.Repositories
{
    public class CountersRepository : GenericRepository<Counter>, ICountersRepository
    {
        public CountersRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
