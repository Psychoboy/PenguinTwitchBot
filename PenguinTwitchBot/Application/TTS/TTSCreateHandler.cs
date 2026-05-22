using PenguinTwitchBot.Application.Alert.Notification;
using PenguinTwitchBot.Bot.Alerts;
using PenguinTwitchBot.Bot.Commands.TTS;
using PenguinTwitchBot.Bot.Notifications;

namespace PenguinTwitchBot.Application.TTS
{
    public class TTSCreateHandler(ITTSPlayerService ttsPlayerService, IWebSocketMessenger webSocketMessenger, ILogger<TTSCreateHandler> logger) : Application.Notifications.INotificationHandler<TTSCreateNotification>
    {
        public async Task Handle(TTSCreateNotification notification, CancellationToken cancellationToken)
        {
            var voice = notification.TTSRequest.RegisteredVoice;
            var message = notification.TTSRequest.Message;
            try
            {
                logger.LogInformation("Creating TTS file for {voice} {type} with message: {message}", voice.Name, voice.Type, message);
                var fileName = await ttsPlayerService.CreateTTSFile(notification.TTSRequest);
                if (string.IsNullOrEmpty(fileName)) return;
                var audioAlert = new AlertSound
                {
                    Path = "tts",
                    AudioHook = fileName
                };
                logger.LogInformation("Queueing TTS file for {voice} {type} with message: {message}", voice.Name, voice.Type, message);
                var alert = new QueueAlert(audioAlert.Generate());
                await webSocketMessenger.AddToQueue(alert.Alert);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating TTS file for {voice} {type} with message: {message}", voice.Name, voice.Type, message);
            }
        }
    }
}
