using DotNetTwitchBot.Application.Alert.Notification;
using DotNetTwitchBot.Bot.Alerts;
using DotNetTwitchBot.Bot.Notifications;
using MediatR;
using YoutubeDLSharp.Options;

namespace DotNetTwitchBot.Application.Clips
{
    public class ClipHandler(ILogger<ClipHandler> logger, IWebSocketMessenger webSocketMessenger) : INotificationHandler<ClipNotification>
    {
        public async Task Handle(ClipNotification request, CancellationToken cancellationToken)
        {
            try
            {
                if (!File.Exists("wwwroot/clips/" + request.Clip.Id + ".mp4"))
                {
                    logger.LogInformation("Downloading clip: {clipUrl}", request.Clip.Url);
                    var ytdl = new YoutubeDLSharp.YoutubeDL();
                    var options = new OptionSet()
                    {
                        Output = "wwwroot/clips/" + request.Clip.Id + ".mp4"
                    };
                    var res = await ytdl.RunVideoDownload(request.Clip.Url, overrideOptions: options);
                    logger.LogInformation("Downloaded clip: {clipUrl}, Path: {path}", request.Clip.Url, res.Data);
                }
                else
                {
                    logger.LogInformation("Clip already exists: {clipUrl}", request.Clip.Url);
                }

                var playClip = new ClipAlert
                {
                    ClipFile = request.Clip.Id + ".mp4",
                    Duration = request.Clip.Duration,
                    StreamerName = request.User.DisplayName,
                    StreamerAvatarUrl = request.User.ProfileImageUrl,
                    GameImageUrl = request.GameUrl
                };
                File.SetLastWriteTime("wwwroot/clips/" + request.Clip.Id + ".mp4", DateTime.Now);
                var alert = new QueueAlert(playClip.Generate());
                await webSocketMessenger.AddToQueue(alert.Alert);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error downloading clip.");
            }
        }
    }
}
