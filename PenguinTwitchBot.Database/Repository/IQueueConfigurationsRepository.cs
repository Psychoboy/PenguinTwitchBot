using PenguinTwitchBot.Database.Bot.Models.Queues;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository
{
    public interface IQueueConfigurationsRepository : IGenericRepository<QueueConfiguration>
    {
    }
}
