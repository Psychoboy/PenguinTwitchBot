using DotNetTwitchBot.Application.Alert.Notification;
using DotNetTwitchBot.BackgroundWorkers;
using DotNetTwitchBot.Bot.Alerts;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.TwitchServices;
using DotNetTwitchBot.Extensions;
using MediatR;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using TwitchLib.Api.Helix.Models.Clips.GetClips;
using YoutubeDLSharp.Options;

namespace DotNetTwitchBot.Bot.Commands.Shoutout
{
    public class ClipService(
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        ILogger<ClipService> logger,
        ITwitchService twitchService,
        IMediator mediator,
        IBackgroundTaskQueue backgroundTaskQueue
        ) : BaseCommandService(serviceBackbone, commandHandler, "Shoutout"), IHostedService, IClipService
    {
        public HttpClient Client { get; private set; }

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
                var user = await twitchService.GetUser(clip.BroadcasterName);
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

                //Queue it to allow processing of other commands 
                backgroundTaskQueue.QueueBackgroundWorkItem(async token => 
                {
                    try
                    {
                        if (!File.Exists("wwwroot/clips/" + clip.Id + ".mp4"))
                        {
                            logger.LogInformation("Downloading clip: {clipUrl}", clip.Url);
                            var ytdl = new YoutubeDLSharp.YoutubeDL();
                            var options = new OptionSet() {
                                Output = "wwwroot/clips/" + clip.Id + ".mp4"
                            };
                            var res = await ytdl.RunVideoDownload(clip.Url, overrideOptions: options);
                            logger.LogInformation("Downloaded clip: {clipUrl}, Path: {path}", clip.Url, res.Data);
                        }

                        var playClip = new ClipAlert
                        {
                            ClipFile = clip.Id + ".mp4",
                            Duration = clip.Duration,
                            StreamerName = user.DisplayName,
                            StreamerAvatarUrl = user.ProfileImageUrl,
                            GameImageUrl = gameUrl
                        };
                        await mediator.Publish(new QueueAlert(playClip.Generate()), token);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error downloading clip.");
                    }
                });
                
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error playing clip.");
            }
        }

        private async Task WatchRequestedClip(CommandEventArgs e)
        {
            var args = Regex.Replace(e.Arg.Trim(), @"[\u0000-\u0008\u000A-\u001F\u0100-\uFFFF]", "");
            if (string.IsNullOrEmpty(args.Trim())) return;
            try
            {
                var url = new Uri(args.Trim());
                if (url.Segments.Length != 4)
                {
                    logger.LogWarning("Not complete segments for !watch. {arg} was used.", e.Arg);
                    return;
                }
                var id = url.Segments[3];
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
            Client = new HttpClient();
            await Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
