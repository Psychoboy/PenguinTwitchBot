using Quartz;

namespace DotNetTwitchBot.Bot.ScheduledJobs
{
    [DisallowConcurrentExecution]
    public class BackupDbJob(IDatabaseTools databaseTools, ILogger<BackupDbJob> logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            var files = Directory.GetFiles("wwwroot/clips/");
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTime < DateTime.Now.AddDays(-30))
                {
                    logger.LogInformation("Deleting File {file} Last Accessed {lastAccessed}", fileInfo.Name, fileInfo.LastAccessTime.ToString());
                    fileInfo.Delete();
                }
            }
            await databaseTools.Backup();
        }
    }
}