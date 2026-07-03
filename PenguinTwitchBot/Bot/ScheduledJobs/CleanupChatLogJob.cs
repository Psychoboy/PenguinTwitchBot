using PenguinTwitchBot.Bot.Core;
using Quartz;

namespace PenguinTwitchBot.Bot.ScheduledJobs
{
    [DisallowConcurrentExecution]
    public class CleanupChatLogJob(IChatHistory chatHistory, ILogger<CleanupChatLogJob> logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            logger.LogInformation("Scheduled Job to clean old chat history started.");
            await chatHistory.CleanOldLogs();
            logger.LogInformation("Scheduled Job to clean old chat history completed.");
        }
    }
}
