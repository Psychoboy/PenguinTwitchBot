using System.Runtime.Serialization.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Alerts;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using System.Text.Json;

namespace DotNetTwitchBot.Bot.Commands.Custom
{
    public class AudioCommands : BaseCommandService
    {
        readonly Dictionary<string, AudioCommand> Commands = new();
        private SendAlerts SendAlerts { get; }
        private ViewerFeature ViewerFeature { get; }
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AudioCommands> _logger;

        public AudioCommands(
            SendAlerts sendAlerts,
            ViewerFeature viewerFeature,
            IServiceScopeFactory scopeFactory,
            ILogger<AudioCommands> logger,
            ServiceBackbone eventService,
            CommandHandler commandHandler) : base(eventService, commandHandler)
        {
            SendAlerts = sendAlerts;
            ViewerFeature = viewerFeature;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        private async Task LoadAudioCommands()
        {
            _logger.LogInformation("Loading Audio Hooks");
            var count = 0;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Commands.Clear(); //Make sure we don't double up
                var commands = await db.AudioCommands.ToListAsync();
                foreach (var command in commands)
                {
                    Commands[command.CommandName] = command;
                    count++;
                }
            }
            _logger.LogInformation("Finished loading Audio Hooks: {0}", count);
        }

        public async Task AddAudioCommand(AudioCommand audioCommand)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                if ((await db.AudioCommands.Where(x => x.CommandName.Equals(audioCommand.CommandName)).FirstOrDefaultAsync()) != null)
                {
                    _logger.LogWarning("Audio command already exists");
                    return;
                }
                await db.AudioCommands.AddAsync(audioCommand);
                Commands[audioCommand.CommandName] = audioCommand;
                await db.SaveChangesAsync();
            }
            await LoadAudioCommands();
        }

        public async Task SaveAudioCommand(AudioCommand audioCommand)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.AudioCommands.Update(audioCommand);
                await db.SaveChangesAsync();
            }
            await LoadAudioCommands();
        }

        public Dictionary<string, AudioCommand> GetAudioCommands()
        {
            return Commands;
        }

        public async Task<AudioCommand?> GetAudioCommand(int id)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.AudioCommands.Where(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task RunCommand(CommandEventArgs e)
        {
            if (Commands.ContainsKey(e.Command) == false) return;
            if (Commands[e.Command].Disabled) return;

            var isCooldownExpired = await CommandHandler.IsCoolDownExpiredWithMessage(e.Name, e.DisplayName, e.Command);
            if (isCooldownExpired == false) return;

            switch (Commands[e.Command].MinimumRank)
            {
                case Rank.Viewer:
                    break; //everyone gets this
                case Rank.Follower:
                    if (await ViewerFeature.IsFollower(e.Name) == false)
                    {
                        await SendChatMessage(e.DisplayName, "you must be a follower to use that command");
                        return;
                    }
                    break;
                case Rank.Subscriber:
                    if (await ViewerFeature.IsSubscriber(e.Name) == false)
                    {
                        await SendChatMessage(e.DisplayName, "you must be a subscriber to use that command");
                        return;
                    }
                    break;
                case Rank.Moderator:
                    if (await ViewerFeature.IsModerator(e.Name) == false)
                    {
                        await SendChatMessage(e.DisplayName, "only moderators can do that...");
                        return;
                    }
                    break;
                case Rank.Streamer:
                    if (ServiceBackbone.IsBroadcasterOrBot(e.Name) == false)
                    {
                        await SendChatMessage(e.DisplayName, "yeah ummm... no... go away");
                        return;
                    }
                    break;
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
            _logger.LogInformation($"Registered {moduleName}");

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

        private void RunCommand(AudioCommand audioCommand, CommandEventArgs e)
        {
            var alertSound = new AlertSound
            {
                AudioHook = audioCommand.AudioFile
            };
            SendAlerts.QueueAlert(alertSound);
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
            if (!Commands.ContainsKey(e.Arg)) return;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var command = await db.AudioCommands.Where(x => x.CommandName.Equals(e.Arg)).FirstOrDefaultAsync();
                if (command == null)
                {
                    await ServiceBackbone.SendChatMessage(string.Format("Failed to enable {0}", e.Arg));
                    return;
                }
                command.Disabled = disabled;
                Commands[e.Arg].Disabled = disabled;
                db.Update(command);
                await db.SaveChangesAsync();
            }
            await ServiceBackbone.SendChatMessage(string.Format("Enabled {0}", e.Arg));
        }

        private async Task RefreshAudio()
        {
            await LoadAudioCommands();
        }

        private async Task AddAudioCommand(CommandEventArgs e)
        {
            try
            {
                var newAudioCommand = JsonSerializer.Deserialize<AudioCommand>(e.Arg);
                if (newAudioCommand != null)
                {
                    await AddAudioCommand(newAudioCommand);
                    await ServiceBackbone.SendChatMessage("Successfully added audio command");
                }
                else
                {
                    await ServiceBackbone.SendChatMessage("Failed to add audio command");
                }

            }
            catch (Exception err)
            {
                _logger.LogError(err, "Failed to add audio command");
            }
        }
    }
}