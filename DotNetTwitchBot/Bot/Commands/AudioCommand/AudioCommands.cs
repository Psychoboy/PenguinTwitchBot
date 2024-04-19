using DotNetTwitchBot.Application.Alert.Notification;
using DotNetTwitchBot.Bot.Alerts;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Repository;
using MediatR;
using System.Collections.Concurrent;
using System.Text.Json;

namespace DotNetTwitchBot.Bot.Commands.AudioCommand
{
    public class AudioCommands(
        IMediator mediator,
        IServiceScopeFactory scopeFactory,
        ILogger<AudioCommands> logger,
        IServiceBackbone eventService,
        ILanguage language,
        ICommandHandler commandHandler) : BaseCommandService(eventService, commandHandler, "AudioHooks"), IHostedService
    {
        readonly ConcurrentDictionary<string, Models.AudioCommand> Commands = [];

        private async Task LoadAudioCommands()
        {
            logger.LogInformation("Loading Audio Hooks");
            var count = 0;
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                Commands.Clear(); //Make sure we don't double up
                var commands = await db.AudioCommands.GetAllAsync();
                foreach (var command in commands)
                {
                    Commands[command.CommandName] = command;
                    count++;
                }
            }
            logger.LogInformation("Finished loading Audio Hooks: {count}", count);
        }

        public async Task AddAudioCommand(Models.AudioCommand audioCommand)
        {
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                if (await db.AudioCommands.Find(x => x.CommandName.Equals(audioCommand.CommandName)).FirstOrDefaultAsync() != null)
                {
                    logger.LogWarning("Audio command already exists");
                    return;
                }
                await db.AudioCommands.AddAsync(audioCommand);
                Commands[audioCommand.CommandName] = audioCommand;
                await db.SaveChangesAsync();
            }
            await LoadAudioCommands();
        }

        public async Task SaveAudioCommand(Models.AudioCommand audioCommand)
        {
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                db.AudioCommands.Update(audioCommand);
                await db.SaveChangesAsync();
            }
            await LoadAudioCommands();
        }

        public async Task DeleteAudioCommand(Models.AudioCommand audioCommand)
        {
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                db.AudioCommands.Remove(audioCommand);
                await db.SaveChangesAsync();
            }
            await LoadAudioCommands();
        }

        public async Task<bool> CommandExists(string command)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.AudioCommands.Find(x => x.CommandName == command).AnyAsync();
        }

        public Dictionary<string, Models.AudioCommand> GetAudioCommands()
        {
            return Commands.ToDictionary();
        }

        public async Task<Models.AudioCommand?> GetAudioCommand(int id)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.AudioCommands.Find(x => x.Id == id).FirstOrDefaultAsync();
        }

        public List<string> GetAudioFiles()
        {
            return Directory.GetFiles("wwwroot/audio")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Distinct()
            .ToList();


        }

        public async Task RunCommand(CommandEventArgs e)
        {
            if (Commands.ContainsKey(e.Command) == false) return;
            if (Commands[e.Command].Disabled) return;

            var isCooldownExpired = await CommandHandler.IsCoolDownExpiredWithMessage(e.Name, e.DisplayName, e.Command);
            if (isCooldownExpired == false) return;

            if (await CommandHandler.CheckPermission(Commands[e.Command], e) == false)
            {
                return;
            }

            RunCommand(Commands[e.Command], e);
        }

        public override async Task Register()
        {
            var moduleName = "AudioCommands";
            await RegisterDefaultCommand("addaudiocommand", this, moduleName, Rank.Streamer);
            await RegisterDefaultCommand("refreshaudiocommands", this, moduleName, Rank.Streamer);
            await RegisterDefaultCommand("disableaudiocommand", this, moduleName, Rank.Streamer);
            await RegisterDefaultCommand("enableaudiocommand", this, moduleName, Rank.Streamer);
            await LoadAudioCommands();
            logger.LogInformation("Registered {moduleName}", moduleName);

        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            if (e.Command.Equals("addaudiocommand"))
            {
                await AddAudioCommand(e);
                return;
            }

            if (e.Command.Equals("refreshaudiocommands"))
            {
                await RefreshAudio();
                return;
            }

            if (e.Command.Equals("disableaudiocommand"))
            {
                await ToggleAudioCommandDisable(e, true);
                return;
            }

            if (e.Command.Equals("enableaudiocommand"))
            {
                await ToggleAudioCommandDisable(e, false);
                return;
            }


        }

        private void RunCommand(Models.AudioCommand audioCommand, CommandEventArgs e)
        {
            var alertSound = new AlertSound
            {
                AudioHook = audioCommand.AudioFile
            };
            mediator.Publish(new QueueAlert(alertSound.Generate()));
            if (Commands[e.Command].GlobalCooldown > 0)
            {
                CommandHandler.AddGlobalCooldown(e.Command, Commands[e.Command].GlobalCooldown);
            }
            if (Commands[e.Command].UserCooldown > 0)
            {
                CommandHandler.AddCoolDown(e.Name, e.Command, Commands[e.Command].UserCooldown);
            }
        }

        private async Task ToggleAudioCommandDisable(CommandEventArgs e, bool disabled)
        {
            if (!Commands.TryGetValue(e.Arg, out Models.AudioCommand? value)) return;
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var command = await db.AudioCommands.Find(x => x.CommandName.Equals(e.Arg)).FirstOrDefaultAsync();
                if (command == null)
                {
                    await ServiceBackbone.SendChatMessage(language.Get("audiocommands.fail.enable").Replace("(command)", e.Arg));
                    return;
                }
                command.Disabled = disabled;
                value.Disabled = disabled;
                db.AudioCommands.Update(command);
                await db.SaveChangesAsync();
            }
            await ServiceBackbone.SendChatMessage(language.Get("audiocommands.success.enable").Replace("(command)", e.Arg));
        }

        private async Task RefreshAudio()
        {
            await LoadAudioCommands();
        }

        private async Task AddAudioCommand(CommandEventArgs e)
        {
            try
            {
                var newAudioCommand = JsonSerializer.Deserialize<Models.AudioCommand>(e.Arg);
                if (newAudioCommand != null)
                {
                    await AddAudioCommand(newAudioCommand);
                    await ServiceBackbone.SendChatMessage(language.Get("audiocommands.success.add"));
                }
                else
                {
                    await ServiceBackbone.SendChatMessage(language.Get("audiocommands.fail.add"));
                }

            }
            catch (Exception err)
            {
                logger.LogError(err, "Failed to add audio command");
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopped {moduledname}", ModuleName);
            return Task.CompletedTask;
        }
    }
}