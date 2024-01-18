namespace DotNetTwitchBot.Repository.Repositories
{
    public class ExternalCommandsRepository : GenericRepository<ExternalCommands>, IExternalCommandsRepository
    {
        public ExternalCommandsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
