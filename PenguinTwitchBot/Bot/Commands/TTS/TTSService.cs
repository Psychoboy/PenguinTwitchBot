using PenguinTwitchBot.Application.Alert.Notification;
using PenguinTwitchBot.Application.TTS;
using PenguinTwitchBot.Bot.Alerts;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Extensions;
using PenguinTwitchBot.Database.Repository;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.TextToSpeech.V1;
using KokoroSharp;
using System.Globalization;

namespace PenguinTwitchBot.Bot.Commands.TTS
{
    public class TTSService(
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        ILogger<TTSService> logger,
        IServiceScopeFactory scopeFactory,
        Application.Notifications.IPenguinDispatcher dispatcher,
        IWebHostEnvironment environment
        ) : BaseCommandService(serviceBackbone, commandHandler, "TTSService", dispatcher), IHostedService, ITTSService
    {
        /// <summary>
        /// Kokoro voices loaded lazily from the NuGet-bundled voice files on first access.
        /// Language code and sex are derived from the standard Kokoro prefix convention.
        /// </summary>
        private static readonly Lazy<IReadOnlyList<RegisteredVoice>> _kokoroVoicesLazy =
            new(BuildKokoroVoices, LazyThreadSafetyMode.ExecutionAndPublication);

        private static IReadOnlyList<RegisteredVoice> KokoroVoices => _kokoroVoicesLazy.Value;

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            if (!command.CommandProperties.CommandName.Equals("say")) return;

            var voices = (await GetUserRegisteredVoices(e.Name)).Select(x => x as RegisteredVoice).ToList();
            if (voices.Count == 0)
            {
                voices = await GetRegisteredVoices();
            }

            var voice = voices.RandomElementOrDefault();
            await SayMessage(voice, e.Name + " says " + e.Arg);
        }

        public async Task SayMessage(RegisteredVoice? voice, string message)
        {
            if (voice is null)
            {
                var voices = await GetRegisteredVoices();
                voice = voices.RandomElementOrDefault();
            }

            if (voice is null)
            {
                // No voices configured at all — attempt a smart locale-based default.
                voice = GetSystemFallbackVoice();
                logger.LogWarning(
                    "No voices configured for TTS. " +
                    "Falling back to Kokoro voice '{VoiceId}' derived from system locale '{Locale}'. " +
                    "Please add voices via the Voices page to suppress this warning.",
                    voice.Name, CultureInfo.CurrentCulture.Name);
            }

            logger.LogInformation("Queueing TTS with voice {Voice} {Type} and message: {Message}", voice.Name, voice.Type, message);
            var request = new TTSRequest
            {
                Message = message,
                RegisteredVoice = voice
            };
            await dispatcher.Publish(new TTSCreateNotification(request));
        }

        public async Task<RegisteredVoice> GetRandomVoice()
        {
            var voices = await GetRegisteredVoices();
            return voices.RandomElementOrDefault();
        }

        public async Task<RegisteredVoice> GetRandomVoice(string name)
        {
            List<RegisteredVoice> voices;
            voices = (await GetUserRegisteredVoices(name)).Select(x => x as RegisteredVoice).ToList();

            if (voices.Count == 0)
            {
                voices = await GetRegisteredVoices();
            }
            return voices.RandomElementOrDefault();
        }

        /// <summary>
        /// Returns voices available to browse and register in the UI.
        /// Kokoro voices are always included (offline, no credentials needed).
        /// Pass <paramref name="includeGoogle"/> = true to also fetch live Google voices
        /// (requires configured Google credentials).
        /// </summary>
        public async Task<List<RegisteredVoice>> GetAllVoices(bool includeGoogle = false)
        {
            var voiceList = new List<RegisteredVoice>(KokoroVoices);

            if (!includeGoogle) return voiceList;

            try
            {
                var credentials = await environment.LoadGoogleCredentialAsync();
                var builder = new TextToSpeechClientBuilder
                {
                    Credential = credentials
                };
                var tts = await builder.BuildAsync();
                var gvoices = await tts.ListVoicesAsync("");
                voiceList.AddRange(gvoices.Voices.Select(x => new RegisteredVoice
                {
                    Type = RegisteredVoice.VoiceType.Google,
                    Name = x.Name,
                    LanguageCode = x.LanguageCodes.First(),
                    Sex = (RegisteredVoice.SexType)x.SsmlGender
                }));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load Google TTS voices. Check your Google credentials configuration.");
            }

            return voiceList;
        }

        // ITTSService keeps the original signature for compatibility; defaults to Kokoro-only.
        async Task<List<RegisteredVoice>> ITTSService.GetAllVoices() => await GetAllVoices(includeGoogle: false);

        public async Task RegisterVoice(RegisteredVoice voice)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var voiceExists = await db.RegisteredVoices.Find(x => x.Name.Equals(voice.Name)).FirstOrDefaultAsync();
            if (voiceExists != null)
            {
                logger.LogWarning("{Name} voice already exists.", voice.Name);
                return;
            }
            await db.RegisteredVoices.AddAsync(voice);
            await db.SaveChangesAsync();
        }

        public async Task DeleteRegisteredVoice(RegisteredVoice voice)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.RegisteredVoices.Remove(voice);
            await db.SaveChangesAsync();
        }

        public async Task RegisterUserVoice(UserRegisteredVoice voice)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await db.UserRegisteredVoices.AddAsync(voice);
            await db.SaveChangesAsync();
        }

        public async Task DeleteRegisteredUserVoice(UserRegisteredVoice voice)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.UserRegisteredVoices.Remove(voice);
            await db.SaveChangesAsync();
        }

        public async Task<List<RegisteredVoice>> GetRegisteredVoices()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return (await db.RegisteredVoices.GetAllAsync()).ToList();
        }

        public async Task<List<UserRegisteredVoice>> GetUserRegisteredVoices()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return (await db.UserRegisteredVoices.GetAllAsync()).ToList();
        }

        public async Task<List<UserRegisteredVoice>> GetUserRegisteredVoices(string username)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.UserRegisteredVoices.Find(x => username.Equals(x.Username)).ToListAsync();
        }

        public override async Task Register()
        {
            await RegisterDefaultCommand("say", this, ModuleName);
            logger.LogInformation("Registered commands for {ModuleName}", ModuleName);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting {Module}", ModuleName);
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopped {Module}", ModuleName);
            return Task.CompletedTask;
        }

        // ─── Kokoro voice catalogue ───────────────────────────────────────────────

        /// <summary>
        /// Picks the best fallback Kokoro voice based on the system locale.
        /// Falls back to <c>af_heart</c> if nothing matches.
        /// </summary>
        private RegisteredVoice GetSystemFallbackVoice()
        {
            var culture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            var region = CultureInfo.CurrentCulture.Name;

            string voiceId = culture switch
            {
                "en" when region.StartsWith("en-GB", StringComparison.OrdinalIgnoreCase) => "bf_emma",
                "en" => "af_heart",
                "es" => "ef_dora",
                "fr" => "ff_siwis",
                "hi" => "hf_alpha",
                "it" => "if_sara",
                "pt" => "pf_dora",
                "ja" => "jf_alpha",
                "zh" => "zf_xiaobei",
                _    => "af_heart"
            };

            return KokoroVoices.FirstOrDefault(v => v.Name == voiceId)
                   ?? KokoroVoices.FirstOrDefault(v => v.Name == "af_heart")
                   ?? KokoroVoices.First();
        }

        /// <summary>
        /// Reads voices directly from <see cref="KokoroVoiceManager"/> (loaded from bundled .npy files)
        /// and derives language code and sex from the standard Kokoro prefix convention:
        /// <c>{lang}{gender}_{name}</c> e.g. <c>af_heart</c> = American Female "heart".
        /// New voices added in future KokoroSharp package updates are automatically discovered.
        /// </summary>
        private static List<RegisteredVoice> BuildKokoroVoices()
        {
            // Ensure the voice files are loaded from the NuGet-bundled "voices/" folder.
            // This is fast (just reads .npy filenames, no model/ONNX needed).
            if (KokoroVoiceManager.Voices.Count == 0)
            {
                try { KokoroVoiceManager.LoadVoicesFromPath(); }
                catch (DirectoryNotFoundException) { /* voices folder not present yet; return empty */ }
            }

            return KokoroVoiceManager.Voices
                .Select(kv => new RegisteredVoice
                {
                    Type = RegisteredVoice.VoiceType.Kokoro,
                    Name = kv.Name,
                    LanguageCode = ResolveLanguageCode(kv.Name),
                    Sex = ResolveSex(kv.Name)
                })
                .OrderBy(v => v.LanguageCode)
                .ThenBy(v => v.Sex)
                .ThenBy(v => v.Name)
                .ToList();
        }

        /// <summary>
        /// Derives the BCP-47 language code from a Kokoro voice name prefix.
        /// Convention: first character = language (a=American English, b=British English,
        /// e=Spanish, f=French, h=Hindi, i=Italian, p=Portuguese, j=Japanese, z=Mandarin).
        /// </summary>
        private static string ResolveLanguageCode(string voiceName) =>
            voiceName.Length >= 2
                ? voiceName[0] switch
                {
                    'a' => "en-US",
                    'b' => "en-GB",
                    'e' => "es",
                    'f' => "fr",
                    'h' => "hi",
                    'i' => "it",
                    'p' => "pt",
                    'j' => "ja",
                    'z' => "zh",
                    _   => "en-US"
                }
                : "en-US";

        /// <summary>
        /// Derives the sex from a Kokoro voice name prefix.
        /// Convention: second character = gender (f=Female, m=Male).
        /// </summary>
        private static RegisteredVoice.SexType ResolveSex(string voiceName) =>
            voiceName.Length >= 2
                ? voiceName[1] switch
                {
                    'f' => RegisteredVoice.SexType.Female,
                    'm' => RegisteredVoice.SexType.Male,
                    _   => RegisteredVoice.SexType.None
                }
                : RegisteredVoice.SexType.None;
    }
}
