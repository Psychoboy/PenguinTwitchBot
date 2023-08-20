namespace DotNetTwitchBot.Bot.Repository.Repositories
{
    public class KnowBotsRepository : GenericRepository<KnownBot>, IKnownBotsRepository
    {
        public KnowBotsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
