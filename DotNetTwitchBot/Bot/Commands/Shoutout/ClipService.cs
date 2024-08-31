using DotNetTwitchBot.Application.Alert.Notification;
using DotNetTwitchBot.Bot.Alerts;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.TwitchServices;
using DotNetTwitchBot.Extensions;
using MediatR;
using System.Linq.Expressions;
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

        private async Task PlayRandomClip(List<Clip> featuredClips)
        {
            var clip = featuredClips.RandomElement();
            var playClip = new ClipAlert
            {
                Duration = (int)Math.Ceiling(clip.Duration),
                ClipUrl = clip.EmbedUrl
            };
            await mediator.Publish(new QueueAlert(playClip.Generate()));
        }

        private async Task WatchRequestedClip(CommandEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Arg)) return;
            try
            {
                var url = new Uri(e.Arg);
                if (url.Segments.Length != 4)
                {
                    logger.LogWarning("Not complete segments for !watch. {arg} was used.", e.Arg);
                    return;
                }
                var id = url.Segments[3];
                var clips = await twitchService.GetClip(id);
                if (clips == null || clips.Count == 0) return;
                var clip = clips.First();
                var playClip = new ClipAlert
                {
                    Duration = (int)Math.Ceiling(clip.Duration),
                    ClipUrl = clip.EmbedUrl
                };
                await mediator.Publish(new QueueAlert(playClip.Generate()));

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

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
