using DotNetTwitchBot.Application.Alert.Notification;
using DotNetTwitchBot.Application.Clips;
using DotNetTwitchBot.Bot.Alerts;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.TwitchServices;
using DotNetTwitchBot.Extensions;
using MediatR;
using System.Text.RegularExpressions;
using TwitchLib.Api.Helix.Models.Clips.GetClips;

namespace DotNetTwitchBot.Bot.Commands.Shoutout
{
    public class ClipService(
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        ILogger<ClipService> logger,
        ITwitchService twitchService,
        IMediator mediator
        ) : BaseCommandService(serviceBackbone, commandHandler, "Shoutout"), IHostedService, IClipService
    {
        public HttpClient Client { get; private set; } = new HttpClient();

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            switch(command.CommandProperties.CommandName)
            {
                case "watch":
                    await WatchRequestedClip(e);
                    break;
                case "stop":
                    await mediator.Publish(new QueueAlert(new StopClip().Generate()));
                    break;
            }
        }

        public async Task PlayRandomClipForStreamer(string streamer)
        {
            try
            {
                Clip? clip = null;
                var featuredClips = await twitchService.GetFeaturedClips(streamer);
                if (featuredClips.Count > 0)
                {
                    clip = GetValidClip(featuredClips);
                }

                var clips = await twitchService.GetClips(streamer);
                if(clip != null && clips.Count > 0)
                {
                    clip = GetValidClip(clips);
                }
                if(clip != null)
                {
                    await PlayClip(clip);
                }
            } catch (Exception ex) {
                logger.LogError(ex, "Error playing random clip");
            } 
        }

        private static Clip? GetValidClip(List<Clip> clips)
        {
            return clips.Where(x => x.Duration < 60.1).RandomElementOrDefault();
            
        }
        private async Task PlayClip(Clip clip)
        {
            try
            {
                var user = await twitchService.GetUserByName(clip.BroadcasterName);
                if (user == null)
                {
                    logger.LogWarning("{user} does not exist.", clip.BroadcasterName);
                    return;
                }
                string gameUrl = "";
        
                var gameInfo = await twitchService.GetGameInfo(clip.GameId);
                if (gameInfo != null)
                {
                    gameUrl = gameInfo.BoxArtUrl;
                    gameUrl = gameUrl.Replace("{width}", "200").Replace("{height}", "200");
                }

                await mediator.Publish(new ClipNotification(clip, user, gameUrl));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error playing clip.");
            }
        }

        private async Task WatchRequestedClip(CommandEventArgs e)
        {
            if (e.Args.Count == 0) return;
            var arg = Regex.Replace(e.Args.First().Trim(), @"[\u0000-\u0008\u000A-\u001F\u0100-\uFFFF]", "");
            if (string.IsNullOrEmpty(arg)) return;
            try
            {
                if(!arg.StartsWith("https://twitch.tv/") && !arg.StartsWith("https://www.twitch.tv/") && !arg.StartsWith("https://clips.twitch.tv/"))
                {
                    logger.LogWarning("Invalid twitch clip URL");
                    return;
                }

                var id = arg.Split('/').Last();
                var clips = await twitchService.GetClip(id);
                if (clips == null || clips.Count == 0) return;
                var clip = clips.First();
                await PlayClip(clip);

            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error doing watch.");
            }
        }

        public override async Task Register()
        {
            await RegisterDefaultCommand("watch", this, ModuleName, Rank.Vip);
            await RegisterDefaultCommand("stop", this, ModuleName, Rank.Viewer);
            logger.LogInformation("Registered commands for {moduleName}", ModuleName);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting {moduledname}", ModuleName);
            await Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopped {moduledname}", ModuleName);
            return Task.CompletedTask;
        }
    }
}
