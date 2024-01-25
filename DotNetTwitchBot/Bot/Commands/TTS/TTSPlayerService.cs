using Google.Apis.Auth.OAuth2;
using Google.Cloud.TextToSpeech.V1;
using NAudio.Wave;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;

namespace DotNetTwitchBot.Bot.Commands.TTS
{
    public class TTSPlayerService : ITTSPlayerService
    {
        public async Task PlayRequest(TTSRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message)) return;
            switch (request.RegisteredVoice.Type)
            {
                case RegisteredVoice.VoiceType.Windows:
                    PlayWindows(request);
                    break;
                case RegisteredVoice.VoiceType.Google:
                    await PlayGoogle(request);
                    break;
            }
        }

        private static async Task PlayGoogle(TTSRequest request)
        {
            var credentials = GoogleCredential.FromFile("gtts.json");
            var builder = new TextToSpeechClientBuilder
            {
                Credential = credentials
            };
            var tts = await builder.BuildAsync();
            var result = await tts.SynthesizeSpeechAsync(
                new SynthesisInput { Text = request.Message },
                new VoiceSelectionParams { LanguageCode = request.RegisteredVoice.LanguageCode, Name = request.RegisteredVoice.Name },
                new AudioConfig { AudioEncoding = AudioEncoding.Mp3 }
                );
            var fileName = Guid.NewGuid().ToString();
            using (var output = File.Create(fileName))
            {
                result.AudioContent.WriteTo(output);
            }

            using var mp3File = File.OpenRead(fileName);
            using var waveOut = new WaveOutEvent();
            using var mp3Reader = new Mp3FileReader(mp3File);
            waveOut.Init(mp3Reader);
            waveOut.Play();
            while (waveOut.PlaybackState == PlaybackState.Playing)
            {
                Thread.Sleep(500);
            }

        }

        private static void PlayWindows(TTSRequest request)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var synthesizer = new SpeechSynthesizer();
                var builder = new PromptBuilder();
                builder.StartVoice(request.RegisteredVoice.Name);
                builder.AppendText(request.Message);
                builder.EndVoice();
                synthesizer.Speak(builder);
            }
        }
    }
}
