
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
                var rawFileName = fileParts[1].Trim();
                var safeBaseName = Path.GetFileNameWithoutExtension(rawFileName);
                if (string.IsNullOrWhiteSpace(safeBaseName)) return Task.CompletedTask;

                var baseDirectory = Path.GetFullPath("wwwroot/tts");
                DeleteFileIfExists(Path.Combine(baseDirectory, safeBaseName + ".mp3"), baseDirectory);
                DeleteFileIfExists(Path.Combine(baseDirectory, safeBaseName + ".wav"), baseDirectory);
            }
            return Task.CompletedTask;
        }

        private void DeleteFileIfExists(string path, string allowedDirectory)
        {
            var fullPath = Path.GetFullPath(path);
            if (!fullPath.StartsWith(allowedDirectory, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Rejected out-of-directory TTS deletion attempt: {Path}", path);
                return;
            }

            if (File.Exists(fullPath))
            {
                logger.LogInformation("Deleting TTS File {Path}", fullPath);
                try
                {
                    File.Delete(fullPath);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete TTS file {Path}", fullPath);
                }
            }
        }
    }
}
