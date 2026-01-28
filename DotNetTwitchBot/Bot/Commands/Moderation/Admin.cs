using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.DatabaseTools;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Notifications;
using MediatR;
using System.IO.Compression;

namespace DotNetTwitchBot.Bot.Commands.Moderation
{
    public class Admin(
        ILogger<Admin> logger,
        IWebSocketMessenger webSocketMessenger,
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        IMediator mediator,
        IServiceScopeFactory scopeFactory
            ) : BaseCommandService(serviceBackbone, commandHandler, "Admin", mediator), IHostedService
    {
        public async Task BackupDatabase()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await BackupTools.BackupDatabase(db, BackupTools.BACKUP_DIRECTORY, logger);
            var files = Directory.GetFiles(BackupTools.BACKUP_DIRECTORY);
            logger.LogInformation("Deleting old backups > 30 days");
            foreach (var file in files)
            {
                FileInfo fi = new(file);
                if (fi.CreationTime < DateTime.Now.AddDays(-30))
                {
                    logger.LogInformation("Deleting backup: {name}", fi.Name);
                    fi.Delete();
                }
            }
        }

        public async Task RestoreDatabase(string fileName)
        {
            var backupDirectory = Path.Combine(Directory.GetCurrentDirectory(), Guid.NewGuid().ToString());
            ZipFile.ExtractToDirectory(fileName, backupDirectory);
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await BackupTools.RestoreDatabase(db, backupDirectory, logger);
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
            await ServiceBackbone.SendChatMessage("Alerts resumed.", PlatformType.Twitch);
        }

        public async Task PauseAlerts()
        {
            webSocketMessenger.Pause();
            await ServiceBackbone.SendChatMessage("Alerts paused.", PlatformType.Twitch);
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