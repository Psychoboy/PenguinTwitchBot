using DotNetTwitchBot.Application.Alert.Notification;
using DotNetTwitchBot.Bot.Alerts;
using DotNetTwitchBot.Bot.Commands.TTS;
using DotNetTwitchBot.Bot.Notifications;
using MediatR;

namespace DotNetTwitchBot.Application.TTS
{
    public class TTSCreateHandler(ITTSPlayerService ttsPlayerService, IWebSocketMessenger webSocketMessenger, ILogger<TTSCreateHandler> logger) : INotificationHandler<TTSCreateNotification>
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
                webSocketMessenger.AddToQueue(alert.Alert);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating TTS file for {voice} {type} with message: {message}", voice.Name, voice.Type, message);
            }
        }
    }
}
