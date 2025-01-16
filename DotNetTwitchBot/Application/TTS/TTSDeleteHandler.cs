using MediatR;

namespace DotNetTwitchBot.Application.TTS
{
    public class TTSDeleteHandler(ILogger<TTSDeleteHandler> logger) : INotificationHandler<TTSDeleteNotification>
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
