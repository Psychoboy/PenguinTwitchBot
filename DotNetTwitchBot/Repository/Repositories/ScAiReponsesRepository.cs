namespace DotNetTwitchBot.Repository.Repositories
{
    public class ScAiReponsesRepository : GenericRepository<ScAiResponseCodes>, IScAiReponsesRepository
    {
        public ScAiReponsesRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
