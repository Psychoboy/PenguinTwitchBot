using System.Runtime.Serialization.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Alerts;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;
using System.Text.Json;

namespace DotNetTwitchBot.Bot.Commands.Custom
{
    public class AudioCommands : BaseCommand
    {
        Dictionary<string, AudioCommand> Commands = new Dictionary<string, AudioCommand>();
        private SendAlerts SendAlerts { get; }
        private ViewerFeature ViewerFeature { get; }
        private readonly IServiceScopeFactory _scopeFactory;
        private ILogger<AudioCommands> _logger;

        public AudioCommands(
            SendAlerts sendAlerts,
            ViewerFeature viewerFeature,
            IServiceScopeFactory scopeFactory,
            ILogger<AudioCommands> logger,
            ServiceBackbone eventService) : base(eventService)
        {
            SendAlerts = sendAlerts;
            ViewerFeature = viewerFeature;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task LoadAudioCommands()
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
        }

        public async Task SaveAudioCommand(AudioCommand audioCommand)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.AudioCommands.Update(audioCommand);
                await db.SaveChangesAsync();
            }
        }

        public Dictionary<string, AudioCommand> GetAudioCommands()
        {
            return Commands;
        }

        public async Task<AudioCommand?> GetAudioCommand(int id)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                return await db.AudioCommands.Where(x => x.Id == id).FirstOrDefaultAsync();
            }
        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            if (e.Command.Equals("addaudiocommand"))
            {
                await AddAudioCommand(e);
                return;
            }

            if (e.Command.Equals("refreshaudiocommands"))
            {
                await RefreshAudio(e.Name);
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

            if (Commands.ContainsKey(e.Command) == false) return;
            if (Commands[e.Command].Disabled) return;

            // if (!IsCoolDownExpired(e.Name, e.Command))
            // {
            //     await _serviceBackbone.SendChatMessage(e.DisplayName, string.Format("That command is still on cooldown: {0}", CooldownLeft(e.Name, e.Command)));
            //     return;
            // }

            var isCooldownExpired = await IsCoolDownExpiredWithMessage(e.Name, e.DisplayName, e.Command);
            if (isCooldownExpired == false) return;

            if (!_serviceBackbone.IsBroadcasterOrBot(e.Name))
            {
                switch (Commands[e.Command].MinimumRank)
                {
                    case Rank.Viewer:
                        break; //everyone gets this
                    case Rank.Follower:
                        if (!(await ViewerFeature.IsFollower(e.Name)))
                        {
                            await SendChatMessage(e.DisplayName, "you must be a follower to use that command");
                            return;
                        }
                        break;
                    case Rank.Subscriber:
                        if (!(await ViewerFeature.IsSubscriber(e.Name)))
                        {
                            await SendChatMessage(e.DisplayName, "you must be a subscriber to use that command");
                            return;
                        }
                        break;
                    case Rank.Moderator:
                        if (!(await ViewerFeature.IsModerator(e.Name)))
                        {
                            await SendChatMessage(e.DisplayName, "only moderators can do that...");
                            return;
                        }
                        break;
                    case Rank.Streamer:
                        await SendChatMessage(e.DisplayName, "yeah ummm... no... go away");
                        return;
                }
            }

            RunCommand(Commands[e.Command], e);
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
                AddGlobalCooldown(e.Command, Commands[e.Command].GlobalCooldown);
            }
            if (Commands[e.Command].UserCooldown > 0)
            {
                AddCoolDown(e.Name, e.Command, Commands[e.Command].UserCooldown);
            }
        }

        private async Task ToggleAudioCommandDisable(CommandEventArgs e, bool disabled)
        {
            if (!_serviceBackbone.IsBroadcasterOrBot(e.Name)) return;
            if (!Commands.ContainsKey(e.Arg)) return;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var command = await db.AudioCommands.Where(x => x.CommandName.Equals(e.Arg)).FirstOrDefaultAsync();
                if (command == null)
                {
                    await _serviceBackbone.SendChatMessage(string.Format("Failed to enable {0}", e.Arg));
                    return;
                }
                command.Disabled = disabled;
                Commands[e.Arg].Disabled = disabled;
                db.Update(command);
                await db.SaveChangesAsync();
            }
            await _serviceBackbone.SendChatMessage(string.Format("Enabled {0}", e.Arg));
        }

        private async Task RefreshAudio(string name)
        {
            if (!_serviceBackbone.IsBroadcasterOrBot(name)) return;
            await LoadAudioCommands();
        }

        private async Task AddAudioCommand(CommandEventArgs e)
        {
            if (!_serviceBackbone.IsBroadcasterOrBot(e.Name)) return;
            try
            {
                var newAudioCommand = JsonSerializer.Deserialize<AudioCommand>(e.Arg);
                if (newAudioCommand != null)
                {
                    await AddAudioCommand(newAudioCommand);
                    await _serviceBackbone.SendChatMessage("Successfully added audio command");
                }
                else
                {
                    await _serviceBackbone.SendChatMessage("Failed to add audio command");
                }

            }
            catch (Exception err)
            {
                _logger.LogError(err, "Failed to add audio command");
            }
        }
    }
}