
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
                var fileName = fileParts[1];
                if (File.Exists("wwwroot/tts/" + fileName.Trim() + ".mp3"))
                {
                    logger.LogInformation("Deleting TTS File {filename}", fileName);
                    File.Delete("wwwroot/tts/" + fileName.Trim() + ".mp3");
                }
            }
            return Task.CompletedTask;
        }
    }
}
