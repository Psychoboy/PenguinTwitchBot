using PenguinTwitchBot.Bot.Core.Database;
using PenguinTwitchBot.Bot.Models.Queues;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class QueueConfigurationsRepository(ApplicationDbContext context) : GenericRepository<QueueConfiguration>(context), IQueueConfigurationsRepository
    {
    }
}
