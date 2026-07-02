using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Database.Bot.DatabaseTools;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Notifications;
using System.IO.Compression;

namespace PenguinTwitchBot.Bot.Commands.Moderation
{
    public class Admin(
        ILogger<Admin> logger,
        IWebSocketMessenger webSocketMessenger,
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        Application.Notifications.IPenguinDispatcher dispatcher,
        IServiceScopeFactory scopeFactory,
        IBackupTools backupTools
            ) : BaseCommandService(serviceBackbone, commandHandler, "Admin", dispatcher), IHostedService
    {
        public async Task BackupDatabase()
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await backupTools.BackupDatabase(db, backupTools.BackupDirectory, logger);

                var settings = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                
                var countSetting = (await settings.Settings.GetAsync(x => x.Name == "BackupCountToKeep")).FirstOrDefault();
                if (countSetting == null)
                {
                    countSetting = new Setting { Name = "BackupCountToKeep", DataType = Setting.DataTypeEnum.Int, IntSetting = 15 };
                    await settings.Settings.AddAsync(countSetting);
                }

                var daysSetting = (await settings.Settings.GetAsync(x => x.Name == "BackupDaysToKeep")).FirstOrDefault();
                if (daysSetting == null)
                {
                    daysSetting = new Setting { Name = "BackupDaysToKeep", DataType = Setting.DataTypeEnum.Int, IntSetting = 15 };
                    await settings.Settings.AddAsync(daysSetting);
                }

                await settings.SaveChangesAsync();
                await backupTools.DeleteOldBackupsAsync(backupTools.BackupDirectory, countSetting.IntSetting, daysSetting.IntSetting, logger);
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "ERROR ERROR Error backing up database! ERROR ERROR");
            }
        }

        public async Task RestoreDatabase(string fileName)
        {
            var backupDirectory = Path.Combine(Directory.GetCurrentDirectory(), Guid.NewGuid().ToString());
            ZipFile.ExtractToDirectory(fileName, backupDirectory);
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await backupTools.RestoreDatabase(db, backupDirectory, logger);
            Directory.Delete(backupDirectory, true);
        }

        public override async Task Register()
        {
            var moduleName = "Admin";
            await RegisterDefaultCommand("pausealerts", this, moduleName, Rank.Streamer);
            await RegisterDefaultCommand("resumealerts", this, moduleName, Rank.Streamer);
            logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            switch (command.CommandProperties.CommandName)
            {
                case "pausealerts":
                    await PauseAlerts();
                    break;
                case "resumealerts":
                    await ResumeAlerts();
                    break;
            }

        }

        public async Task ResumeAlerts()
        {
            webSocketMessenger.Resume();
            await ServiceBackbone.SendChatMessage("Alerts resumed.");
        }

        public async Task PauseAlerts()
        {
            webSocketMessenger.Pause();
            await ServiceBackbone.SendChatMessage("Alerts paused.");
        }

        public async Task ForceStreamOnline()
        {
            ServiceBackbone.IsOnline = true;
            await ServiceBackbone.OnStreamStarted();
        }

        public async Task ForceStreamOffline()
        {
            ServiceBackbone.IsOnline = false;
            await ServiceBackbone.OnStreamEnded();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting {moduledname}", ModuleName);
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopped {moduledname}", ModuleName);
            return Task.CompletedTask;
        }
    }
}
