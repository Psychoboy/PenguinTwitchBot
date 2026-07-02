using PenguinTwitchBot.Services;

namespace PenguinTwitchBot.Services;

public interface IFileCleanupService
{
    Task CleanupTtsAsync();
    Task CleanupClipsAsync();
}

public class FileCleanupService(
    IScheduledJobSettingsService scheduledJobSettingsService,
    IWebHostEnvironment webHostEnvironment,
    ILogger<FileCleanupService> logger) : IFileCleanupService
{
    public async Task CleanupTtsAsync()
    {
        var maxAgeHours = await scheduledJobSettingsService.GetTtsCleanupMaxAgeHoursAsync(1);
        maxAgeHours = Math.Max(0, maxAgeHours);
        var cutoff = DateTime.Now.AddHours(-maxAgeHours);
        await CleanupDirectoryAsync(Path.Combine(webHostEnvironment.WebRootPath, "tts"), cutoff, maxAgeHours == 0, "TTS");
    }

    public async Task CleanupClipsAsync()
    {
        var maxAgeDays = await scheduledJobSettingsService.GetClipsCleanupMaxAgeDaysAsync(30);
        maxAgeDays = Math.Max(0, maxAgeDays);
        var cutoff = DateTime.Now.AddDays(-maxAgeDays);
        await CleanupDirectoryAsync(Path.Combine(webHostEnvironment.WebRootPath, "clips"), cutoff, maxAgeDays == 0, "clip");
    }

    private Task CleanupDirectoryAsync(string directoryPath, DateTime cutoff, bool deleteAll, string label)
    {
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var files = Directory.GetFiles(directoryPath);
        foreach (var file in files)
        {
            var fileInfo = new FileInfo(file);
            if (deleteAll || fileInfo.LastWriteTime < cutoff)
            {
                try
                {
                    logger.LogInformation("Deleting {Label} file {FileName}; LastWriteTime={LastWriteTime}", label, fileInfo.Name, fileInfo.LastWriteTime);
                    fileInfo.Delete();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete {Label} file {FileName}; skipping.", label, fileInfo.Name);
                }
            }
        }

        return Task.CompletedTask;
    }
}
