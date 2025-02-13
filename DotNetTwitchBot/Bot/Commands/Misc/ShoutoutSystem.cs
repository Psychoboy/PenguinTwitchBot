using DotNetTwitchBot.Bot.Commands.Shoutout;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.TwitchServices;
using DotNetTwitchBot.Repository;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class ShoutoutSystem : BaseCommandService, IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ITwitchService _twitchService;
        private readonly ILogger<ShoutoutSystem> _logger;
        private readonly IClipService _clipService;
        private DateTime LastShoutOut = DateTime.Now;
        private readonly Dictionary<string, DateTime> UserLastShoutout = [];

        public ShoutoutSystem(
            ILogger<ShoutoutSystem> logger,
            IServiceScopeFactory scopeFactory,
            ITwitchService twitchService,
            IServiceBackbone serviceBackbone,
            ICommandHandler commandHandler,
            IClipService clipService
            ) : base(serviceBackbone, commandHandler, "ShoutoutSystem")
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
            var autoShoutExists = await db.AutoShoutouts.Find(x => x.Name.Equals(autoShoutout.Name)).FirstOrDefaultAsync();
            if (autoShoutExists != null)
            {
                _logger.LogWarning("{name} autoshoutout already exists.", autoShoutout.Name);
                return;
            }
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
            var name = e.Name;
            AutoShoutout? autoShoutout = null;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var beforeTime = DateTime.Now.AddHours(-12);
                autoShoutout = await db.AutoShoutouts.Find(x => x.Name.Equals(name) && x.LastShoutout < beforeTime).FirstOrDefaultAsync();
            }
            if (autoShoutout != null)
            {
                await Shoutout(name, autoShoutout.AutoPlayClip);
            }
        }

        private async Task Shoutout(string name, bool playClip)
        {
            AutoShoutout? autoShoutout = null;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                autoShoutout = await db.AutoShoutouts.Find(x => x.Name.Equals(name.ToLower())).FirstOrDefaultAsync();
            }

            await UpdateLastShoutout(autoShoutout);

            var message = "Go give (name) a follow at https://twitch.tv/(name) - They were last seen playing (game)!";
            if (autoShoutout != null && !string.IsNullOrWhiteSpace(autoShoutout.CustomMessage))
            {
                message = autoShoutout.CustomMessage;
            }

            var userId = await _twitchService.GetUserId(name);
            if (userId == null) return;

            var game = await _twitchService.GetCurrentGame(userId);
            if (string.IsNullOrWhiteSpace(game)) game = "Some boring game";

            message = message.Replace("(name)", name).Replace("(game)", game);
            await ServiceBackbone.SendChatMessage(message);
            if(playClip) await _clipService.PlayRandomClipForStreamer(name);
            
            await TwitchShoutOut(userId);
            
        }


        private async Task TwitchShoutOut(string userId)
        {

            if (LastShoutOut.AddMinutes(2) < DateTime.Now)
            {
                if (UserLastShoutout.TryGetValue(userId, out DateTime value))
                {
                    if (value.AddMinutes(60) > DateTime.Now) return;
                }
                if (ServiceBackbone.IsOnline == false) return;
                var result = await _twitchService.ShoutoutStreamer(userId);
                if (result == ShoutoutResponseEnum.Success)
                {
                    LastShoutOut = DateTime.Now;
                    UserLastShoutout[userId] = DateTime.Now;
                }
            }
        }

        private async Task UpdateLastShoutout(AutoShoutout? autoShoutout)
        {
            if (autoShoutout == null) return;
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            autoShoutout.LastShoutout = DateTime.Now;
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
                    await Shoutout(e.TargetUser, true);
                    break;
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopped {moduledname}", ModuleName);
            return Task.CompletedTask;
        }
    }
}