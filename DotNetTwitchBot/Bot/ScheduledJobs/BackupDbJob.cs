using Quartz;

namespace DotNetTwitchBot.Bot.ScheduledJobs
{
    [DisallowConcurrentExecution]
    public class BackupDbJob(IDatabaseTools databaseTools, ILogger<BackupDbJob> logger) : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            var files = Directory.GetFiles("wwwroot/clips/");
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastAccessTime < DateTime.Now.AddDays(-30))
                {
                    logger.LogInformation("Deleting File {file} Last Accessed {lastAccessed}", fileInfo.Name, fileInfo.LastAccessTime.ToString());
                    fileInfo.Delete();
                }
            }
            return databaseTools.Backup();
        }
    }
}