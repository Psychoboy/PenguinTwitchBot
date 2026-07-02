using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Services;

public interface IScheduledJobSettingsService
{
    public const string TriggerBackupJobName = "TriggerBackupJob";
    public const string CleanupClipsJobName = "CleanupClipsJob";
    public const string CleanupChatLogsJobName = "CleanupChatLogsJob";
    public const string CleanupIpLogsJobName = "CleanupIpLogsJob";
    public const string TtsCleanupJobName = "TtsCleanupJob";
    public const string UpdateDiscordEventsJobName = "UpdateDiscordEvents";
    public const string PostScheduleJobName = "PostSchedule";
    public const string UpdatePostedScheduleJobName = "UpdatePostedSchedule";
    public const string ValidationSanityCheckJobName = "ValidationSanityCheck";

    Task<bool> GetJobEnabledAsync(string jobName, bool defaultValue = true);
    Task<string> GetJobCronAsync(string jobName, string defaultValue);
    Task SetJobEnabledAsync(string jobName, bool value);
    Task SetJobCronAsync(string jobName, string value);

    Task<bool> GetRunOnStartupAsync(string jobName, bool defaultValue = true);
    Task SetRunOnStartupAsync(string jobName, bool value);

    Task<int> GetTtsCleanupMaxAgeHoursAsync(int defaultValue = 1);
    Task SetTtsCleanupMaxAgeHoursAsync(int value);
    Task<int> GetClipsCleanupMaxAgeDaysAsync(int defaultValue = 30);
    Task SetClipsCleanupMaxAgeDaysAsync(int value);
    Task<int> GetIpLogMonthsToKeepAsync(int defaultValue = 6);
    Task SetIpLogMonthsToKeepAsync(int value);
}

public class ScheduledJobSettingsService(IServiceScopeFactory scopeFactory) : IScheduledJobSettingsService
{
    private const int MaxSaveRetries = 5;

    public Task<bool> GetJobEnabledAsync(string jobName, bool defaultValue = true) =>
        GetBoolSettingAsync($"ScheduledJob.{jobName}.Enabled", defaultValue);

    public Task<string> GetJobCronAsync(string jobName, string defaultValue) =>
        GetStringSettingAsync($"ScheduledJob.{jobName}.Cron", defaultValue);

    public Task SetJobEnabledAsync(string jobName, bool value) =>
        SetBoolSettingAsync($"ScheduledJob.{jobName}.Enabled", value);

    public Task SetJobCronAsync(string jobName, string value) =>
        SetStringSettingAsync($"ScheduledJob.{jobName}.Cron", value);

    public Task<bool> GetRunOnStartupAsync(string jobName, bool defaultValue = true) =>
        GetBoolSettingAsync($"ScheduledJob.{jobName}.RunOnStartup", defaultValue);

    public Task SetRunOnStartupAsync(string jobName, bool value) =>
        SetBoolSettingAsync($"ScheduledJob.{jobName}.RunOnStartup", value);

    public Task<int> GetTtsCleanupMaxAgeHoursAsync(int defaultValue = 1) =>
        GetIntSettingAsync("Cleanup.Tts.MaxAgeHours", defaultValue);

    public Task SetTtsCleanupMaxAgeHoursAsync(int value) =>
        SetIntSettingAsync("Cleanup.Tts.MaxAgeHours", value);

    public Task<int> GetClipsCleanupMaxAgeDaysAsync(int defaultValue = 30) =>
        GetIntSettingAsync("Cleanup.Clips.MaxAgeDays", defaultValue);

    public Task SetClipsCleanupMaxAgeDaysAsync(int value) =>
        SetIntSettingAsync("Cleanup.Clips.MaxAgeDays", value);

    public Task<int> GetIpLogMonthsToKeepAsync(int defaultValue = 6) =>
        GetIntSettingAsync("Cleanup.IpLogs.MonthsToKeep", defaultValue);

    public Task SetIpLogMonthsToKeepAsync(int value) =>
        SetIntSettingAsync("Cleanup.IpLogs.MonthsToKeep", value);

    private async Task<bool> GetBoolSettingAsync(string name, bool defaultValue)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var setting = (await db.Settings.GetAsync(x => x.Name == name)).FirstOrDefault();
        return setting == null ? defaultValue : setting.IntSetting != 0;
    }

    private async Task<string> GetStringSettingAsync(string name, string defaultValue)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var setting = (await db.Settings.GetAsync(x => x.Name == name)).FirstOrDefault();
        return string.IsNullOrWhiteSpace(setting?.StringSetting) ? defaultValue : setting.StringSetting;
    }

    private async Task<int> GetIntSettingAsync(string name, int defaultValue)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var setting = (await db.Settings.GetAsync(x => x.Name == name)).FirstOrDefault();
        return setting?.IntSetting ?? defaultValue;
    }

    private Task SetBoolSettingAsync(string name, bool value) => SetIntSettingAsync(name, value ? 1 : 0);

    private async Task SetStringSettingAsync(string name, string value)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var setting = (await db.Settings.GetAsync(x => x.Name == name)).FirstOrDefault();
        if (setting == null)
        {
            setting = new Setting
            {
                Name = name,
                DataType = Setting.DataTypeEnum.String,
                StringSetting = value
            };
            await db.Settings.AddAsync(setting);
        }
        else
        {
            setting.DataType = Setting.DataTypeEnum.String;
            setting.StringSetting = value;
            db.Settings.Update(setting);
        }

        await SaveChangesWithRetryAsync(db);
    }

    private async Task SetIntSettingAsync(string name, int value)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var setting = (await db.Settings.GetAsync(x => x.Name == name)).FirstOrDefault();
        if (setting == null)
        {
            setting = new Setting
            {
                Name = name,
                DataType = Setting.DataTypeEnum.Int,
                IntSetting = value
            };
            await db.Settings.AddAsync(setting);
        }
        else
        {
            setting.DataType = Setting.DataTypeEnum.Int;
            setting.IntSetting = value;
            db.Settings.Update(setting);
        }

        await SaveChangesWithRetryAsync(db);
    }

    private static async Task SaveChangesWithRetryAsync(IUnitOfWork db)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.SaveChangesAsync();
                return;
            }
            catch (DbUpdateException ex) when (IsSqliteTableLock(ex) && attempt < MaxSaveRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt));
            }
        }
    }

    private static bool IsSqliteTableLock(DbUpdateException ex)
    {
        return ex.InnerException is SqliteException sqliteEx && sqliteEx.SqliteErrorCode == 6;
    }

}
