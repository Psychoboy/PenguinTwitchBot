
namespace PenguinTwitchBot.Application.TTS
{
#pragma warning disable S101 // Types should be named in PascalCase
    public class TTSDeleteHandler(ILogger<TTSDeleteHandler> logger) : Application.Notifications.INotificationHandler<TTSDeleteNotification>
#pragma warning restore S101 // Types should be named in PascalCase
    {
        public Task Handle(TTSDeleteNotification notification, CancellationToken cancellationToken)
        {
            var data = notification.Data;
            var fileParts = data.Split(":");
            if (fileParts.Length > 1)
            {
                var fileName = fileParts[1].Trim();
                DeleteFileIfExists("wwwroot/tts/" + fileName + ".mp3");
                DeleteFileIfExists("wwwroot/tts/" + fileName + ".wav");
            }
            return Task.CompletedTask;
        }

        private void DeleteFileIfExists(string path)
        {
            if (File.Exists(path))
            {
                logger.LogInformation("Deleting TTS File {Path}", path);
                try
                {
                    File.Delete(path);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete TTS file {Path}", path);
                }
            }
        }
    }
}
