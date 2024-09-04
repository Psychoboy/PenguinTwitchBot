using DotNetTwitchBot.Application.Alert.Notification;
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
                var featuredClips = await twitchService.GetFeaturedClips(streamer);
                if (featuredClips.Count > 0)
                {
                    await PlayRandomClip(featuredClips);
                    return;
                }

                var clips = await twitchService.GetClips(streamer);
                if(clips.Count > 0)
                {
                    await PlayRandomClip(clips);
                    return;
                }
            } catch (Exception ex) {
                logger.LogError(ex, "Error playing random clip");
            } 
        }

        private async Task PlayRandomClip(List<Clip> clips)
        {
            var clip = clips.RandomElement();
            await PlayClip(clip);
        }
        private async Task PlayClip(Clip clip)
        {
            if (!File.Exists("wwwroot/clips/" + clip.Id + ".mp4"))
            {
 

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        FileName = "youtube-dl.exe",
                        Arguments = "-o wwwroot/clips/" + clip.Id + ".mp4 " + clip.Url
                    }
                };

                process.Start();
                process.WaitForExit();
            }


            var playClip = new ClipAlert
            {
                ClipFile = clip.Id + ".mp4",
                Duration = clip.Duration
            };
            await mediator.Publish(new QueueAlert(playClip.Generate()));
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
