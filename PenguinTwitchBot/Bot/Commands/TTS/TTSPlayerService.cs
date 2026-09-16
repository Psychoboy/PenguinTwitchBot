using Google.Cloud.TextToSpeech.V1;
using Grpc.Core;
using KokoroSharp;
using PenguinTwitchBot.Extensions;

namespace PenguinTwitchBot.Bot.Commands.TTS
{
    public class TTSPlayerService(ILogger<TTSPlayerService> logger, IWebHostEnvironment environment) : ITTSPlayerService
    {
        private const string DefaultGeminiTtsModel = "gemini-2.5-flash-tts";

        // Kokoro voice to use when falling back from a failed Google TTS attempt.
        private const string KokoroFallbackVoiceId = "af_heart";

        // Lazy-loaded Kokoro synthesizer — created on first use, cached for the service lifetime.
        private KokoroWavSynthesizer? _kokoroSynth;
        private readonly SemaphoreSlim _kokoroInitLock = new(1, 1);

        public async Task<string> CreateTTSFile(TTSRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message)) return string.Empty;
            if (!Directory.Exists("wwwroot/tts/"))
            {
                Directory.CreateDirectory("wwwroot/tts/");
            }

            // Clean up any old orphaned TTS files older than 15 minutes
            CleanupOldTTSFiles(TimeSpan.FromMinutes(15));

            switch (request.RegisteredVoice.Type)
            {
                case RegisteredVoice.VoiceType.Google:
                    var googleResult = await PlayGoogle(request);
                    if (!string.IsNullOrEmpty(googleResult)) return googleResult;

                    // Google failed — fall back to Kokoro
                    logger.LogWarning(
                        "Google TTS failed for voice {Voice}; falling back to Kokoro voice '{FallbackVoice}'.",
                        request.RegisteredVoice.Name, KokoroFallbackVoiceId);
                    return await PlayKokoro(request.Message, KokoroFallbackVoiceId);

                case RegisteredVoice.VoiceType.Kokoro:
                    return await PlayKokoro(request.Message, request.RegisteredVoice.Name);

                default:
                    logger.LogWarning("Invalid VoiceType: {VoiceType}", request.RegisteredVoice.Type);
                    return string.Empty;
            }
        }

        public void DeleteTTSFile(string fileNameOrRelativeUrl)
        {
            if (string.IsNullOrWhiteSpace(fileNameOrRelativeUrl)) return;
            try
            {
                var cleanName = Path.GetFileName(fileNameOrRelativeUrl.Split('?')[0]);
                var baseName = Path.GetFileNameWithoutExtension(cleanName);
                var wavPath = Path.Combine("wwwroot", "tts", baseName + ".wav");
                var mp3Path = Path.Combine("wwwroot", "tts", baseName + ".mp3");

                if (File.Exists(wavPath))
                {
                    logger.LogInformation("Deleting TTS file: {Path}", wavPath);
                    File.Delete(wavPath);
                }
                if (File.Exists(mp3Path))
                {
                    logger.LogInformation("Deleting TTS file: {Path}", mp3Path);
                    File.Delete(mp3Path);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete TTS file {FileName}", fileNameOrRelativeUrl);
            }
        }

        public void CleanupOldTTSFiles(TimeSpan maxAge)
        {
            try
            {
                const string dir = "wwwroot/tts/";
                if (!Directory.Exists(dir)) return;

                var cutoff = DateTime.UtcNow - maxAge;
                var files = Directory.GetFiles(dir);
                foreach (var file in files)
                {
                    var fi = new FileInfo(file);
                    if (fi.CreationTimeUtc < cutoff && fi.LastWriteTimeUtc < cutoff)
                    {
                        try
                        {
                            logger.LogInformation("Cleaning up expired TTS file: {FileName}", fi.Name);
                            fi.Delete();
                        }
                        catch (Exception ex)
                        {
                            logger.LogDebug(ex, "Could not delete expired TTS file {FileName}", fi.Name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error during TTS cleanup");
            }
        }

        // ─── Google TTS ──────────────────────────────────────────────────────────

        private async Task<string> PlayGoogle(TTSRequest request)
        {
            try
            {
                var credentials = await environment.LoadGoogleCredentialAsync();
                var builder = new TextToSpeechClientBuilder
                {
                    Credential = credentials
                };
                var tts = await builder.BuildAsync();
                logger.LogInformation("Starting to compile Google Voice: {VoiceName}", request.RegisteredVoice.Name);
                var voiceSelectionParams = new VoiceSelectionParams
                {
                    LanguageCode = request.RegisteredVoice.LanguageCode,
                    Name = request.RegisteredVoice.Name
                };
                SynthesizeSpeechResponse result;
                try
                {
                    result = await tts.SynthesizeSpeechAsync(
                        new SynthesisInput { Text = request.Message },
                        voiceSelectionParams,
                        new AudioConfig { AudioEncoding = AudioEncoding.Mp3 }
                        );
                }
                catch (RpcException ex) when (ex.Status.Detail.Contains("requires a model name", StringComparison.OrdinalIgnoreCase))
                {
                    voiceSelectionParams.ModelName = DefaultGeminiTtsModel;
                    result = await tts.SynthesizeSpeechAsync(
                        new SynthesisInput { Text = request.Message },
                        voiceSelectionParams,
                        new AudioConfig { AudioEncoding = AudioEncoding.Mp3 }
                        );
                }
                var fileName = Guid.NewGuid().ToString();
                using (var output = File.Create("wwwroot/tts/" + fileName + ".mp3"))
                {
                    result.AudioContent.WriteTo(output);
                }
                logger.LogInformation("Saved Google TTS file: {Filename}", fileName);
                return fileName;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create Google TTS message.");
                return string.Empty;
            }
        }

        // ─── Kokoro TTS ──────────────────────────────────────────────────────────

        /// <summary>
        /// Synthesizes speech using KokoroSharp (offline, CPU-only).
        /// The ONNX model is downloaded on first use and cached locally by the library.
        /// Subsequent calls reuse the in-memory synthesizer instance.
        /// </summary>
        private async Task<string> PlayKokoro(string message, string voiceId)
        {
            try
            {
                var synth = await GetOrInitKokoroAsync();
                if (synth is null)
                {
                    logger.LogError("Kokoro synthesizer could not be initialized; skipping TTS.");
                    return string.Empty;
                }

                logger.LogInformation("Starting to compile Kokoro voice: {VoiceId}", voiceId);

                var voice = KokoroVoiceManager.GetVoice(voiceId);
                if (voice is null)
                {
                    logger.LogError("Kokoro voice '{VoiceId}' not found; skipping TTS.", voiceId);
                    return string.Empty;
                }

                // Synthesize on a background thread — ONNX inference is CPU-bound.
                var audioBytes = await Task.Run(() => synth.Synthesize(message, voice));

                var fileName = Guid.NewGuid().ToString();
                var filePath = "wwwroot/tts/" + fileName + ".wav";
                KokoroWavSynthesizer.SaveAudioToFile(audioBytes, filePath);

                logger.LogInformation("Saved Kokoro TTS file: {Filename}", fileName);
                return fileName;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create Kokoro TTS message for voice '{VoiceId}'.", voiceId);
                return string.Empty;
            }
        }

        /// <summary>
        /// Returns the shared <see cref="KokoroWavSynthesizer"/> instance, initializing it lazily on first call.
        /// Thread-safe via <see cref="_kokoroInitLock"/>.
        /// </summary>
        private async Task<KokoroWavSynthesizer?> GetOrInitKokoroAsync()
        {
            if (_kokoroSynth is not null) return _kokoroSynth;

            await _kokoroInitLock.WaitAsync();
            try
            {
                if (_kokoroSynth is not null) return _kokoroSynth;

                logger.LogInformation(
                    "Initializing Kokoro TTS engine (first use). " +
                    "If the model has not been downloaded yet this may take a moment.");

                _kokoroSynth = await Task.Run(() => KokoroWavSynthesizer.LoadModel());
                logger.LogInformation("Kokoro TTS engine ready.");
                return _kokoroSynth;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to initialize Kokoro TTS engine. " +
                    "Ensure the model can be downloaded or is already cached.");
                return null;
            }
            finally
            {
                _kokoroInitLock.Release();
            }
        }
    }
}
