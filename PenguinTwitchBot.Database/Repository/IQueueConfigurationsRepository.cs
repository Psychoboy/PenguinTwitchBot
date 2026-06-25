using PenguinTwitchBot.Bot.Models.Queues;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository
{
    public interface IQueueConfigurationsRepository : IGenericRepository<QueueConfiguration>
    {
    }
}
