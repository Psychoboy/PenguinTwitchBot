using Google.Apis.Auth.OAuth2;
using Google.Cloud.TextToSpeech.V1;

namespace DotNetTwitchBot.Bot.Commands.TTS
{
    public class TTSPlayerService(ILogger<TTSPlayerService> logger) : ITTSPlayerService
    {
        private string LastFileName = string.Empty;
        public async Task<string> CreateTTSFile(TTSRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message)) return string.Empty;
            if (Directory.Exists("wwwroot/tts/") == false)
            {
                Directory.CreateDirectory("wwwroot/tts/");
            }
            switch (request.RegisteredVoice.Type)
            {
                case RegisteredVoice.VoiceType.Google:
                    return await PlayGoogle(request);
                default:
                    logger.LogWarning("Invalid VoiceType: {voiceType}", request.RegisteredVoice.Type);
                    return string.Empty;
            }
        }

        private async Task<string> PlayGoogle(TTSRequest request)
        {
            try
            {
                var credentials = GoogleCredential.FromFile("gtts.json");
                var builder = new TextToSpeechClientBuilder
                {
                    Credential = credentials
                };
                var tts = await builder.BuildAsync();
                logger.LogInformation("Starting to compile Google Voice: {voiceName}", request.RegisteredVoice.Name);
                var result = await tts.SynthesizeSpeechAsync(
                    new SynthesisInput { Text = request.Message },
                    new VoiceSelectionParams { LanguageCode = request.RegisteredVoice.LanguageCode, Name = request.RegisteredVoice.Name },
                    new AudioConfig { AudioEncoding = AudioEncoding.Mp3 }
                    );
                var fileName = Guid.NewGuid().ToString();
                using (var output = File.Create("wwwroot/tts/" + fileName + ".mp3"))
                {
                    result.AudioContent.WriteTo(output);
                }
                logger.LogInformation("Saved TTS file: {filename}", fileName);
                return fileName;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create google message.");
                return string.Empty;
            }
        }
    }
}
