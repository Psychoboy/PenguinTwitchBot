using DotNetTwitchBot.Bot.Core.Database;
using DotNetTwitchBot.Bot.Models.Queues;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class QueueConfigurationsRepository(ApplicationDbContext context) : GenericRepository<QueueConfiguration>(context), IQueueConfigurationsRepository
    {
    }
}
