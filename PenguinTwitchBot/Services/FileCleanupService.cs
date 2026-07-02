using PenguinTwitchBot.Services;

namespace PenguinTwitchBot.Services;

public interface IFileCleanupService
{
    Task CleanupTtsAsync();
    Task CleanupClipsAsync();
}

public class FileCleanupService(
    IScheduledJobSettingsService scheduledJobSettingsService,
    ILogger<FileCleanupService> logger) : IFileCleanupService
{
    public async Task CleanupTtsAsync()
    {
        var maxAgeHours = await scheduledJobSettingsService.GetTtsCleanupMaxAgeHoursAsync(1);
        maxAgeHours = Math.Max(0, maxAgeHours);

        var directoryPath = "wwwroot/tts/";
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var files = Directory.GetFiles(directoryPath);
        var cutoff = DateTime.Now.AddHours(-maxAgeHours);
        foreach (var file in files)
        {
            var fileInfo = new FileInfo(file);
            if (maxAgeHours == 0 || fileInfo.LastWriteTime < cutoff)
            {
                logger.LogInformation("Deleting TTS file {FileName}; LastWriteTime={LastWriteTime}", fileInfo.Name, fileInfo.LastWriteTime);
                fileInfo.Delete();
            }
        }
    }

    public async Task CleanupClipsAsync()
    {
        var maxAgeDays = await scheduledJobSettingsService.GetClipsCleanupMaxAgeDaysAsync(30);
        maxAgeDays = Math.Max(0, maxAgeDays);

        var directoryPath = "wwwroot/clips/";
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var files = Directory.GetFiles(directoryPath);
        var cutoff = DateTime.Now.AddDays(-maxAgeDays);
        foreach (var file in files)
        {
            var fileInfo = new FileInfo(file);
            if (maxAgeDays == 0 || fileInfo.LastWriteTime < cutoff)
            {
                logger.LogInformation("Deleting clip file {FileName}; LastWriteTime={LastWriteTime}", fileInfo.Name, fileInfo.LastWriteTime);
                fileInfo.Delete();
            }
        }
    }
}
