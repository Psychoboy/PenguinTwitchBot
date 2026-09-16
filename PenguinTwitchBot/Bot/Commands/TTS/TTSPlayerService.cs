using Google.Cloud.TextToSpeech.V1;
using Grpc.Core;
using KokoroSharp;
using PenguinTwitchBot.Extensions;

namespace PenguinTwitchBot.Bot.Commands.TTS
{
    public class TTSPlayerService(
        ILogger<TTSPlayerService> logger,
        IWebHostEnvironment environment,
        PenguinTwitchBot.Services.ITTSSettingsService ttsSettingsService,
        IPiperService piperService) : ITTSPlayerService
    {
        private const string DefaultGeminiTtsModel = "gemini-2.5-flash-tts";

        // Kokoro voice to use when falling back from a failed Google TTS attempt.
        private const string KokoroFallbackVoiceId = "af_heart";

        private sealed class KokoroInstance : IDisposable
        {
            private readonly object _lock = new();
            public KokoroWavSynthesizer Synthesizer { get; }
            private int _activeOperations;
            private bool _isRetired;

            public KokoroInstance(KokoroWavSynthesizer synthesizer)
            {
                Synthesizer = synthesizer;
            }

            public bool TryEnter()
            {
                lock (_lock)
                {
                    if (_isRetired) return false;
                    _activeOperations++;
                    return true;
                }
            }

            public void Exit()
            {
                bool shouldDispose = false;
                lock (_lock)
                {
                    _activeOperations--;
                    if (_isRetired && _activeOperations <= 0)
                    {
                        shouldDispose = true;
                    }
                }
                if (shouldDispose)
                {
                    DisposeSynthesizer();
                }
            }

            public void Retire()
            {
                bool shouldDispose = false;
                lock (_lock)
                {
                    _isRetired = true;
                    if (_activeOperations <= 0)
                    {
                        shouldDispose = true;
                    }
                }
                if (shouldDispose)
                {
                    DisposeSynthesizer();
                }
            }

            private void DisposeSynthesizer()
            {
                try
                {
                    Synthesizer.Dispose();
                }
                catch (Exception)
                {
                    // Synthesizer disposal is best-effort upon instance retirement
                }
            }

            public void Dispose() => Retire();
        }

        // Lazy-loaded Kokoro instance — created on first use, tracked for safe lifecycle & reload.
        private KokoroInstance? _kokoroInstance;
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
                case BaseVoice.VoiceType.Google:
                    var googleResult = await PlayGoogle(request);
                    if (!string.IsNullOrEmpty(googleResult)) return googleResult;

                    // Google failed — fall back to Kokoro matching the requested language
                    var fallbackVoice = ResolveKokoroFallbackVoice(request.RegisteredVoice.LanguageCode);
                    logger.LogWarning(
                        "Google TTS failed for voice {Voice}; falling back to Kokoro voice '{FallbackVoice}'.",
                        request.RegisteredVoice.Name, fallbackVoice);
                    return await PlayKokoro(request.Message, fallbackVoice);

                case BaseVoice.VoiceType.Kokoro:
                    return await PlayKokoro(request.Message, request.RegisteredVoice.Name);

                case BaseVoice.VoiceType.Piper:
                    return await PlayPiper(request.Message, request.RegisteredVoice.Name);

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
                var instance = await GetOrInitKokoroInstanceAsync();
                if (instance is null || !instance.TryEnter())
                {
                    // Instance may have been retired during a concurrent settings reload.
                    // Retry once to pick up the fresh replacement.
                    instance = await GetOrInitKokoroInstanceAsync();
                    if (instance is null || !instance.TryEnter())
                    {
                        logger.LogError("Kokoro synthesizer could not be initialized or acquired; skipping TTS.");
                        return string.Empty;
                    }
                }

                try
                {
                    logger.LogInformation("Starting to compile Kokoro voice: {VoiceId}", voiceId);

                    var voice = KokoroVoiceManager.GetVoice(voiceId);
                    if (voice is null)
                    {
                        logger.LogError("Kokoro voice '{VoiceId}' not found; skipping TTS.", voiceId);
                        return string.Empty;
                    }

                    // Synthesize on a background thread — ONNX inference is CPU-bound.
                    var audioBytes = await Task.Run(() => instance.Synthesizer.Synthesize(message, voice));

                    var fileName = Guid.NewGuid().ToString();
                    var filePath = "wwwroot/tts/" + fileName + ".wav";
                    KokoroWavSynthesizer.SaveAudioToFile(audioBytes, filePath);

                    logger.LogInformation("Saved Kokoro TTS file: {Filename}", fileName);
                    return fileName;
                }
                finally
                {
                    instance.Exit();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create Kokoro TTS message for voice '{VoiceId}'.", voiceId);
                return string.Empty;
            }
        }

        // ─── Piper TTS ──────────────────────────────────────────────────────────

        private async Task<string> PlayPiper(string message, string modelKey)
        {
            try
            {
                logger.LogInformation("Starting to compile Piper voice: {ModelKey}", modelKey);
                var audioBytes = await piperService.SynthesizeAsync(message, modelKey);

                if (audioBytes == null || audioBytes.Length <= 44)
                {
                    logger.LogError("Piper synthesis failed: audio data is empty or invalid ({Length} bytes) for voice '{ModelKey}'.", audioBytes?.Length ?? 0, modelKey);
                    return string.Empty;
                }

                var fileName = Guid.NewGuid().ToString();
                var filePath = Path.Combine("wwwroot", "tts", $"{fileName}.wav");
                await File.WriteAllBytesAsync(filePath, audioBytes);

                logger.LogInformation("Saved Piper TTS file: {Filename}", fileName);
                return fileName;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create Piper TTS message for voice '{ModelKey}'.", modelKey);
                return string.Empty;
            }
        }

        // ─── Kokoro Lifecycle & Settings ──────────────────────────────────────────

        /// <summary>
        /// Returns the shared <see cref="KokoroInstance"/>, initializing it lazily on first call.
        /// Thread-safe via <see cref="_kokoroInitLock"/>.
        /// </summary>
        private async Task<KokoroInstance?> GetOrInitKokoroInstanceAsync(bool forceReload = false)
        {
            if (_kokoroInstance is not null && !forceReload) return _kokoroInstance;

            await _kokoroInitLock.WaitAsync();
            try
            {
                if (_kokoroInstance is not null && !forceReload) return _kokoroInstance;

                return await InitKokoroLockedAsync();
            }
            finally
            {
                _kokoroInitLock.Release();
            }
        }

        private async Task<KokoroInstance?> InitKokoroLockedAsync()
        {
            try
            {
                var threads = await ttsSettingsService.GetKokoroThreadsAsync(2);
                var clampedThreads = Math.Clamp(threads, 1, Environment.ProcessorCount);

                logger.LogInformation(
                    "Initializing Kokoro TTS engine (configured threads: {Threads}). " +
                    "If the model has not been downloaded yet this may take a moment.", clampedThreads);

                var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions
                {
                    IntraOpNumThreads = clampedThreads,
                    InterOpNumThreads = 1,
                    ExecutionMode = Microsoft.ML.OnnxRuntime.ExecutionMode.ORT_SEQUENTIAL
                };
                var synth = await Task.Run(() => KokoroWavSynthesizer.LoadModel(sessionOptions: sessionOptions));
                _kokoroInstance = new KokoroInstance(synth);

                logger.LogInformation("Kokoro TTS engine ready with {Threads} threads.", clampedThreads);
                return _kokoroInstance;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to initialize Kokoro TTS engine. " +
                    "Ensure the model can be downloaded or is already cached.");
                return null;
            }
        }

        public async Task ReloadKokoroSettingsAsync()
        {
            await _kokoroInitLock.WaitAsync();
            try
            {
                var oldInstance = _kokoroInstance;
                _kokoroInstance = null;
                oldInstance?.Retire();

                await InitKokoroLockedAsync();
            }
            finally
            {
                _kokoroInitLock.Release();
            }
        }

        private static string ResolveKokoroFallbackVoice(string? languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode)) return KokoroFallbackVoiceId;

            var lang = languageCode.Trim().ToLowerInvariant();
            var separatorIdx = lang.IndexOfAny(['-', '_']);
            var prefix = separatorIdx >= 0 ? lang[..separatorIdx] : lang;

            return prefix switch
            {
                "en" when lang.StartsWith("en-gb", StringComparison.OrdinalIgnoreCase) => "bf_emma",
                "en" => "af_heart",
                "es" => "ef_dora",
                "fr" => "ff_siwis",
                "hi" => "hf_alpha",
                "it" => "if_sara",
                "pt" => "pf_dora",
                "ja" => "jf_alpha",
                "zh" => "zf_xiaobei",
                _ => KokoroFallbackVoiceId
            };
        }
    }
}
