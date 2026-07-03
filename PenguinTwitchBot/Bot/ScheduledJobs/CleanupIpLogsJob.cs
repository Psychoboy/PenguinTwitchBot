using PenguinTwitchBot.Circuit;
using Quartz;

namespace PenguinTwitchBot.Bot.ScheduledJobs
{
    [DisallowConcurrentExecution]
    public class CleanupIpLogsJob(IpLog ipLog, ILogger<CleanupIpLogsJob> logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            logger.LogInformation("Scheduled Job to clean old IP logs started.");
            await ipLog.CleanupOldIpLogs();
            logger.LogInformation("Scheduled Job to clean old IP logs completed.");
        }
    }
}
