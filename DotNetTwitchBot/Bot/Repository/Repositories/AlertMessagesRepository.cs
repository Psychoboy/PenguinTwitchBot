namespace DotNetTwitchBot.Bot.Repository.Repositories
{
    public class AlertMessagesRepository : GenericRepository<AlertMessage>, IAlertMessagesRepository
    {
        public AlertMessagesRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
