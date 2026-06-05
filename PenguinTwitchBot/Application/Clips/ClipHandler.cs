using PenguinTwitchBot.Application.Alert.Notification;
using PenguinTwitchBot.Bot.Alerts;
using PenguinTwitchBot.Bot.Notifications;
using YoutubeDLSharp.Options;

namespace PenguinTwitchBot.Application.Clips
{
    public class ClipHandler(ILogger<ClipHandler> logger, IWebSocketMessenger webSocketMessenger) : Notifications.INotificationHandler<ClipNotification>
    {
        public async Task Handle(ClipNotification request, CancellationToken cancellationToken)
        {
            try
            {
                var clipsDir = "wwwroot/clips/";
                var absoluteClipsDir = Path.GetFullPath(clipsDir);
                logger.LogInformation("Clip directory: {absoluteClipsDir} (cwd: {cwd})", absoluteClipsDir, Directory.GetCurrentDirectory());
                Directory.CreateDirectory(clipsDir);
                var clipPath = clipsDir + request.Clip.Id + ".mp4";

                if (!File.Exists(clipPath))
                {
                    logger.LogInformation("Downloading clip: {clipUrl}, AbsolutePath: {path}", request.Clip.Url, Path.GetFullPath(clipPath));
                    var ytdl = new YoutubeDLSharp.YoutubeDL();
                    var options = new OptionSet()
                    {
                        Output = clipPath
                    };
                    var res = await ytdl.RunVideoDownload(request.Clip.Url, overrideOptions: options);
                    logger.LogInformation("Download result — Success: {success}, Data: {data}, Errors: {errors}", res.Success, res.Data, string.Join("; ", res.ErrorOutput ?? []));
                    if (!res.Success || !File.Exists(clipPath))
                    {
                        logger.LogError("Clip download failed, skipping playback.");
                        return;
                    }
                }
                else
                {
                    logger.LogInformation("Clip already exists: {clipUrl}, Path: {path}", request.Clip.Url, Path.GetFullPath(clipPath));
                }

                var playClip = new ClipAlert
                {
                    ClipFile = request.Clip.Id + ".mp4",
                    Duration = request.Clip.Duration,
                    StreamerName = request.User.DisplayName,
                    StreamerAvatarUrl = request.User.ProfileImageUrl ?? "",
                    GameImageUrl = request.GameUrl
                };
                File.SetLastWriteTime(clipPath, DateTime.Now);
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
