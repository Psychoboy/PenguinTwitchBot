using PenguinTwitchBot.Bot.Ai;
using PenguinTwitchBot.Bot.Commands.Shoutout;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Repository;

namespace PenguinTwitchBot.Bot.Commands.Misc
{
    public class ShoutoutSystem : BaseCommandService, IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ITwitchService _twitchService;
        private readonly ILogger<ShoutoutSystem> _logger;
        private readonly IClipService _clipService;
        private DateTime LastShoutOut = DateTime.UtcNow;
        private readonly Dictionary<string, DateTime> UserLastShoutout = [];

        public ShoutoutSystem(
            ILogger<ShoutoutSystem> logger,
            IServiceScopeFactory scopeFactory,
            ITwitchService twitchService,
            IServiceBackbone serviceBackbone,
            ICommandHandler commandHandler,
            Application.Notifications.IPenguinDispatcher dispatcher,
            IClipService clipService
            ) : base(serviceBackbone, commandHandler, "ShoutoutSystem", dispatcher)
        {
            _scopeFactory = scopeFactory;
            _twitchService = twitchService;
            _logger = logger;
            _clipService = clipService;
        }

        public async Task<List<AutoShoutout>> GetAutoShoutoutsAsync()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.AutoShoutouts.GetAsync(orderBy: (x => x.OrderBy(y => y.Name)));
        }

        public async Task AddAutoShoutout(AutoShoutout autoShoutout)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var normalizedName = UsernameNormalizer.Normalize(autoShoutout.Name);
            var autoShoutExists = await db.AutoShoutouts.Find(x => x.Name.Equals(normalizedName)).FirstOrDefaultAsync();
            if (autoShoutExists != null)
            {
                _logger.LogWarning("{name} autoshoutout already exists.", normalizedName);
                return;
            }
            autoShoutout.Name = normalizedName;
            await db.AutoShoutouts.AddAsync(autoShoutout);
            await db.SaveChangesAsync();
        }

        public async Task DeleteAutoShoutout(AutoShoutout autoShoutout)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.AutoShoutouts.Remove(autoShoutout);
            await db.SaveChangesAsync();
        }

        public async Task OnChatMessage(ChatMessageEventArgs e)
        {
            if (ServiceBackbone.IsOnline == false) return;
            var normalizedName = UsernameNormalizer.Normalize(e.Name);
            AutoShoutout? autoShoutout = null;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var beforeTime = DateTime.UtcNow.AddHours(-12);
                autoShoutout = await db.AutoShoutouts.Find(x => x.Name.Equals(normalizedName) && x.LastShoutout < beforeTime).FirstOrDefaultAsync();
            }
            if (autoShoutout != null)
            {
                await Shoutout(normalizedName, autoShoutout.AutoPlayClip, true, false);
            }
        }

        private async Task Shoutout(string name, bool playClip, bool useAi, bool sourceOnly)
        {
            AutoShoutout? autoShoutout = null;
            var normalizedName = UsernameNormalizer.Normalize(name);
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                autoShoutout = await db.AutoShoutouts.Find(x => x.Name.Equals(normalizedName)).FirstOrDefaultAsync();
            }

            await UpdateLastShoutout(autoShoutout);
            var userId = await _twitchService.GetUserId(name);
            if (userId == null) return;

            var game = await _twitchService.GetCurrentGame(userId);
            if (string.IsNullOrWhiteSpace(game)) game = "Some boring game";

            var message = "";
            if((autoShoutout != null && autoShoutout.UseAi) || useAi)
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var streamTitle = await _twitchService.GetUserStreamTitle(userId);
                    var bio = await _twitchService.GetUserBio(userId);
                    var shoutoutAi = scope.ServiceProvider.GetService<IShoutoutAi>();
                    if (shoutoutAi != null)
                    {
                        message = await shoutoutAi.GetShoutoutForStreamer(
                            name,
                            game ?? "Unknown Game",
                            streamTitle ?? "No Title",
                            bio ?? "No Bio",
                            autoShoutout?.AdditionalPrompt ?? string.Empty
                        );
                    }
                } catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating AI shoutout for {name}", name);
                }
            }

            if (string.IsNullOrWhiteSpace(message))
            {

                message = "Go give (name) a follow at https://twitch.tv/(name) - They were last seen playing (game)!";
                if (autoShoutout != null && !string.IsNullOrWhiteSpace(autoShoutout.CustomMessage))
                {
                    message = autoShoutout.CustomMessage;
                }
            } else
            {
                message = message.Trim() + " https://twitch.tv/(name)";
            }

                message = message.Replace("(name)", name).Replace("(game)", game);
            await ServiceBackbone.SendChatMessage(message, false);
            if(playClip) await _clipService.PlayRandomClipForStreamer(name);
            
            await TwitchShoutOut(userId);
            
        }


        private async Task TwitchShoutOut(string userId)
        {

            if (LastShoutOut.AddMinutes(2) < DateTime.UtcNow)
            {
                if (UserLastShoutout.TryGetValue(userId, out DateTime value))
                {
                    if (value.AddMinutes(60) > DateTime.UtcNow) return;
                }
                if (ServiceBackbone.IsOnline == false) return;
                var result = await _twitchService.ShoutoutStreamer(userId);
                if (result == ShoutoutResponseEnum.Success)
                {
                    LastShoutOut = DateTime.UtcNow;
                    UserLastShoutout[userId] = DateTime.UtcNow;
                }
            }
        }

        private async Task UpdateLastShoutout(AutoShoutout? autoShoutout)
        {
            if (autoShoutout == null) return;
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            autoShoutout.LastShoutout = DateTime.UtcNow;
            db.AutoShoutouts.Update(autoShoutout);
            await db.SaveChangesAsync();
        }

        public async Task<AutoShoutout?> GetAutoShoutoutAsync(int id)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.AutoShoutouts.Find(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task UpdateAutoShoutoutAsync(AutoShoutout autoShoutout)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.AutoShoutouts.Update(autoShoutout);
            await db.SaveChangesAsync();
        }

        public override async Task Register()
        {
            var moduleName = "ShoutoutSystem";
            await RegisterDefaultCommand("shoutout", this, moduleName, Rank.Vip);
            await RegisterDefaultCommand("so", this, moduleName, Rank.Vip);
            _logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            switch (command.CommandProperties.CommandName)
            {
                case "shoutout":
                case "so":
                    if (e.Args.Count != 0 == false)
                    {
                        throw new SkipCooldownException();
                    }
                    await Shoutout(e.TargetUser, true, true, command.CommandProperties.SourceOnly);
                    break;
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting {moduledname}", ModuleName);
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopped {moduledname}", ModuleName);
            return Task.CompletedTask;
        }
    }
}