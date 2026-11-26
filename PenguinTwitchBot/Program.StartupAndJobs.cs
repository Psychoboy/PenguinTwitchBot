using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Circuit;
using Quartz;

internal partial class Program
{
    private static async Task RunStartupInitializationAsync(WebApplication app, ILogger<Program>? programLogger)
    {
        var url = app.Urls.FirstOrDefault()?.TrimEnd('/')
            ?? app.Configuration["Kestrel:Endpoints:Http:Url"]?.TrimEnd('/')
            ?? "http://localhost:5000";

        if (Uri.TryCreate(url, UriKind.Absolute, out var parsedUrl))
        {
            if (parsedUrl.Host == "0.0.0.0" || parsedUrl.Host == "[::]" || parsedUrl.Host == "::")
            {
                var port = parsedUrl.IsDefaultPort ? "" : $":{parsedUrl.Port}";
                url = $"{parsedUrl.Scheme}://localhost{port}";
            }
        }

        programLogger?.LogInformation("Bot Started");
        programLogger?.LogInformation("Connect to the bot at {Url}", url);

        var appLifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        _ = Task.Factory.StartNew(async () =>
        {
            try
            {
                // Defer optional startup cleanup so the bot can become responsive first.
                await Task.Delay(TimeSpan.FromSeconds(10), appLifetime.ApplicationStopping);
                await RunConfiguredStartupCleanupJobsAsync(app.Services, programLogger, appLifetime.ApplicationStopping);
            }
            catch (OperationCanceledException)
            {
                // App is stopping; no cleanup needed.
            }
            catch (Exception ex)
            {
                programLogger?.LogError(ex, "Startup cleanup task failed.");
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

        await Task.CompletedTask;
    }

    private static async Task RunConfiguredStartupCleanupJobsAsync(
        IServiceProvider serviceProvider,
        ILogger<Program>? programLogger,
        CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IScheduledJobSettingsService>();
        var fileCleanupService = scope.ServiceProvider.GetRequiredService<IFileCleanupService>();
        var chatHistory = scope.ServiceProvider.GetRequiredService<IChatHistory>();
        var ipLog = scope.ServiceProvider.GetRequiredService<PenguinTwitchBot.Circuit.IpLog>();
        var cooldownCleanupService = scope.ServiceProvider.GetRequiredService<ICooldownCleanupService>();

        stoppingToken.ThrowIfCancellationRequested();
        if (await settings.GetJobEnabledAsync(IScheduledJobSettingsService.CleanupChatLogsJobName, true) &&
            await settings.GetRunOnStartupAsync(IScheduledJobSettingsService.CleanupChatLogsJobName, true))
        {
            programLogger?.LogInformation("Running startup cleanup: chat logs");
            await chatHistory.CleanOldLogs();
        }

        stoppingToken.ThrowIfCancellationRequested();
        if (await settings.GetJobEnabledAsync(IScheduledJobSettingsService.CleanupIpLogsJobName, true) &&
            await settings.GetRunOnStartupAsync(IScheduledJobSettingsService.CleanupIpLogsJobName, true))
        {
            programLogger?.LogInformation("Running startup cleanup: IP logs");
            await ipLog.CleanupOldIpLogs();
        }

        stoppingToken.ThrowIfCancellationRequested();
        if (await settings.GetJobEnabledAsync(IScheduledJobSettingsService.CleanupCooldownsJobName, true) &&
            await settings.GetRunOnStartupAsync(IScheduledJobSettingsService.CleanupCooldownsJobName, true))
        {
            programLogger?.LogInformation("Running startup cleanup: expired cooldowns");
            await cooldownCleanupService.CleanupExpiredCooldownsAsync();
        }

        stoppingToken.ThrowIfCancellationRequested();
        if (await settings.GetJobEnabledAsync(IScheduledJobSettingsService.TtsCleanupJobName, true) &&
            await settings.GetRunOnStartupAsync(IScheduledJobSettingsService.TtsCleanupJobName, true))
        {
            programLogger?.LogInformation("Running startup cleanup: TTS files");
            await fileCleanupService.CleanupTtsAsync();
        }

        stoppingToken.ThrowIfCancellationRequested();
        if (await settings.GetJobEnabledAsync(IScheduledJobSettingsService.CleanupClipsJobName, true) &&
            await settings.GetRunOnStartupAsync(IScheduledJobSettingsService.CleanupClipsJobName, true))
        {
            programLogger?.LogInformation("Running startup cleanup: clip files");
            await fileCleanupService.CleanupClipsAsync();
        }
        programLogger?.LogInformation("Startup cleanup completed.");
    }

    private static async Task ConfigureQuartzTriggersAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IScheduledJobSettingsService>();
        var schedulerFactory = scope.ServiceProvider.GetRequiredService<ISchedulerFactory>();
        var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var scheduler = await schedulerFactory.GetScheduler();

        var defaultCronByJob = new Dictionary<string, string>
        {
            [IScheduledJobSettingsService.TriggerBackupJobName] = "0 0 12 * * ?",
            [IScheduledJobSettingsService.CleanupClipsJobName] = "0 0 12 * * ?",
            [IScheduledJobSettingsService.CleanupChatLogsJobName] = "0 0 13 * * ?",
            [IScheduledJobSettingsService.CleanupIpLogsJobName] = "0 30 13 * * ?",
            [IScheduledJobSettingsService.CleanupCooldownsJobName] = "0 0/30 * * * ?",
            [IScheduledJobSettingsService.TtsCleanupJobName] = "0 0 * * * ?",
            [IScheduledJobSettingsService.UpdateDiscordEventsJobName] = "0 0/5 * * * ?",
            [IScheduledJobSettingsService.PostScheduleJobName] = "0 0 9 ? * MON",
            [IScheduledJobSettingsService.UpdatePostedScheduleJobName] = "0 0 10 * * ?",
            [IScheduledJobSettingsService.ValidationSanityCheckJobName] = "0 0 5 * * ?",
            [IScheduledJobSettingsService.RefreshEmoteCacheJobName] = "0 0 3 * * ?"
        };

        foreach (var kvp in defaultCronByJob)
        {
            var enabled = await settings.GetJobEnabledAsync(kvp.Key, true);
            var configuredCron = await settings.GetJobCronAsync(kvp.Key, kvp.Value);
            var isValidCron = CronExpression.TryParse(configuredCron, out _);
            var cronToUse = isValidCron ? configuredCron : kvp.Value;
            if (!isValidCron)
            {
                scopedLogger.LogWarning("Invalid cron '{Cron}' for job {JobName}. Falling back to default '{DefaultCron}'.", configuredCron, kvp.Key, kvp.Value);
                await settings.SetJobCronAsync(kvp.Key, kvp.Value);
            }

            await UpsertCronTriggerAsync(scheduler, new JobKey(kvp.Key), new TriggerKey($"{kvp.Key}-Trigger"), enabled, cronToUse);
        }

        // Warm the emote cache shortly after startup, once Twitch services have had time to connect.
        var emoteJobKey = new JobKey(IScheduledJobSettingsService.RefreshEmoteCacheJobName);
        if (await settings.GetJobEnabledAsync(IScheduledJobSettingsService.RefreshEmoteCacheJobName, true))
        {
            var startupTriggerKey = new TriggerKey($"{IScheduledJobSettingsService.RefreshEmoteCacheJobName}-Startup");
            await scheduler.UnscheduleJob(startupTriggerKey);
            await scheduler.ScheduleJob(TriggerBuilder.Create()
                .WithIdentity(startupTriggerKey)
                .ForJob(emoteJobKey)
                .StartAt(DateTimeOffset.UtcNow.AddSeconds(15))
                .Build());
        }
    }

    private static async Task UpsertCronTriggerAsync(
        IScheduler scheduler,
        JobKey jobKey,
        TriggerKey triggerKey,
        bool enabled,
        string cronExpression)
    {
        var existingTrigger = await scheduler.GetTrigger(triggerKey);
        if (existingTrigger != null)
        {
            await scheduler.UnscheduleJob(triggerKey);
        }

        if (!enabled)
        {
            return;
        }

        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey)
            .WithCronSchedule(cronExpression)
            .Build();

        await scheduler.ScheduleJob(trigger);
    }
}
