using DotNetTwitchBot.BackgroundWorkers;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Repository;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.TextToSpeech.V1;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;

namespace DotNetTwitchBot.Bot.Commands.TTS
{
    public class TTSService(
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        ILogger<TTSService> logger,
        IServiceScopeFactory scopeFactory,
        ITTSPlayerService ttsPlayerService,
        IBackgroundTaskQueue backgroundTaskQueue
        ) : BaseCommandService(serviceBackbone, commandHandler, "TTSService"), IHostedService, ITTSService
    {
        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            if (!command.CommandProperties.CommandName.Equals("say")) return;

            List<RegisteredVoice> voices;
            voices = (await GetUserRegisteredVoices(e.Name)).Select(x => x as RegisteredVoice).ToList();

            if (voices.Count == 0)
            {
                voices = await GetRegisteredVoices();
            }

            if (voices.Count == 0)
            {
                logger.LogWarning("No voices configured for TTS.");
                return;
            }

            var voice = voices.RandomElement();
            SayMessage(voice, e.Name + " says " + e.Arg);
        }

        public void SayMessage(RegisteredVoice voice, string message)
        {

            var request = new TTSRequest
            {
                Message = message,
                RegisteredVoice = voice
            };
            backgroundTaskQueue.QueueBackgroundWorkItem(async token =>
            {
                await ttsPlayerService.PlayRequest(request);
            });
        }

        public async Task<RegisteredVoice> GetRandomVoice()
        {
            var voices = await GetRegisteredVoices();
            return voices.RandomElement();
        }

        public async Task<RegisteredVoice> GetRandomVoice(string name)
        {
            List<RegisteredVoice> voices;
            voices = (await GetUserRegisteredVoices(name)).Select(x => x as RegisteredVoice).ToList();

            if (voices.Count == 0)
            {
                voices = await GetRegisteredVoices();
            }
            return voices.RandomElement();
        }

        public async Task<List<RegisteredVoice>> GetAllVoices()
        {
            List<RegisteredVoice> voiceList = [];
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var synthesizer = new SpeechSynthesizer();
                var voices = synthesizer.GetInstalledVoices().ToList();
#pragma warning disable CA1416 // Validate platform compatibility
                voiceList.AddRange(voices.Select(x => new RegisteredVoice
                {
                    Type = RegisteredVoice.VoiceType.Windows,
                    Name = x.VoiceInfo.Name,
                    Sex = (RegisteredVoice.SexType)x.VoiceInfo.Gender
                }));
#pragma warning restore CA1416 // Validate platform compatibility
            }
            var credentials = GoogleCredential.FromFile("gtts.json");
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
            return voiceList;
        }

        public async Task RegisterVoice(RegisteredVoice voice)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var voiceExists = await db.RegisteredVoices.Find(x => x.Name.Equals(voice.Name)).FirstOrDefaultAsync();
            if (voiceExists != null)
            {
                logger.LogWarning("{name} voice already exists.", voice.Name);
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
            logger.LogInformation("Registered commands for {moduleName}", ModuleName);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
