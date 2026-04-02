using DotNetTwitchBot.Bot.Models.Queues;

namespace DotNetTwitchBot.Bot.Queues
{
    public interface IQueueManager
    {
        IActionExecutionLogger ExecutionLogger { get; }

        Task<IActionQueue> GetQueueAsync(string queueName);
        Task<QueueConfiguration> CreateQueueAsync(QueueConfiguration config);
        Task<QueueConfiguration> UpdateQueueAsync(QueueConfiguration config);
        Task DeleteQueueAsync(string queueName);
        Task<List<QueueConfiguration>> GetAllQueuesAsync();
        Task<QueueStatistics> GetQueueStatisticsAsync(string queueName);
        Task<List<QueueStatistics>> GetAllQueueStatisticsAsync();
        Task StartAllQueuesAsync(CancellationToken cancellationToken);
        Task StopAllQueuesAsync();
    }
}
