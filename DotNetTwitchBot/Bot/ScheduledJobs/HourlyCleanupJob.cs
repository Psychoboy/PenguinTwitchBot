using Quartz;

namespace DotNetTwitchBot.Bot.ScheduledJobs
{
    public class HourlyCleanupJob(ILogger<HourlyCleanupJob> logger) : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            var files = Directory.GetFiles("wwwroot/tts/");
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTime < DateTime.Now.AddHours(-1))
                {
                    logger.LogInformation("Deleting TTS {file} Last Created {lastAccessed}", fileInfo.Name, fileInfo.LastWriteTime.ToString());
                    fileInfo.Delete();
                }
            }
            return Task.CompletedTask;
        }
    }
}
