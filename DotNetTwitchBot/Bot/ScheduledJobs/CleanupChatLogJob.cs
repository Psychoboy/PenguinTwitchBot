using DotNetTwitchBot.Bot.Core;
using Quartz;

namespace DotNetTwitchBot.Bot.ScheduledJobs
{
    [DisallowConcurrentExecution]
    public class CleanupChatLogJob(IChatHistory chatHistory, ILogger<CleanupChatLogJob> logger) : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            logger.LogInformation("Scheduled Job to clean old chat history started.");
            return chatHistory.CleanOldLogs();
        }
    }
}
