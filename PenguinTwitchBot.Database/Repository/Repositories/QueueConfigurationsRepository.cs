using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models.Queues;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class QueueConfigurationsRepository(ApplicationDbContext context) : GenericRepository<QueueConfiguration>(context), IQueueConfigurationsRepository
    {
    }
}
