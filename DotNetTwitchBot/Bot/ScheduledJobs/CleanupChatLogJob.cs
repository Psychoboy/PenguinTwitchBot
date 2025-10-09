using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Circuit;
using Quartz;

namespace DotNetTwitchBot.Bot.ScheduledJobs
{
    [DisallowConcurrentExecution]
    public class CleanupChatLogJob(IChatHistory chatHistory, IpLog ipLog, ILogger<CleanupChatLogJob> logger) : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            logger.LogInformation("Scheduled Job to clean old chat history started.");
            var tasks = new List<Task>
            {
                chatHistory.CleanOldLogs(),
                ipLog.CleanupOldIpLogs()
            };
            return Task.WhenAll(tasks);
        }
    }
}
