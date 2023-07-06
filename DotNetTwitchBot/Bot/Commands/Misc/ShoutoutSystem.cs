using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.TwitchServices;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class ShoutoutSystem : BaseCommand
    {
        private IServiceScopeFactory _scopeFactory;
        private TwitchService _twitchService;
        private ILogger<ShoutoutSystem> _logger;
        private DateTime LastShoutOut = DateTime.Now;
        private Dictionary<string, DateTime> UserLastShoutout = new Dictionary<string, DateTime>();

        public ShoutoutSystem(
            ILogger<ShoutoutSystem> logger,
            IServiceScopeFactory scopeFactory,
            TwitchServices.TwitchService twitchService,
            ServiceBackbone serviceBackbone
            ) : base(serviceBackbone)
        {
            serviceBackbone.ChatMessageEvent += OnChatMessage;
            _scopeFactory = scopeFactory;
            _twitchService = twitchService;
            _logger = logger;
        }

        public async Task<List<AutoShoutout>> GetAutoShoutoutsAsync()
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                return await db.AutoShoutouts.OrderBy(x => x.Name).ToListAsync();
            }
        }

        public async Task AddAutoShoutout(AutoShoutout autoShoutout)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var autoShoutExists = await db.AutoShoutouts.Where(x => x.Name.Equals(autoShoutout.Name)).FirstOrDefaultAsync();
                if (autoShoutExists != null)
                {
                    _logger.LogWarning("{0} autoshoutout already exists.", autoShoutout.Name);
                    return;
                }
                await db.AutoShoutouts.AddAsync(autoShoutout);
                await db.SaveChangesAsync();
            }
        }

        private async Task OnChatMessage(object? sender, ChatMessageEventArgs e)
        {
            if (_serviceBackbone.IsOnline == false) return;
            var name = e.Name;
            AutoShoutout? autoShoutout = null;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var beforeTime = DateTime.Now.AddHours(-12);
                autoShoutout = await db.AutoShoutouts.Where(x => x.Name.Equals(name) && x.LastShoutout < beforeTime).FirstOrDefaultAsync();
            }
            if (autoShoutout != null)
            {
                await Shoutout(name);
            }
        }

        private async Task Shoutout(string name)
        {
            AutoShoutout? autoShoutout = null;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                autoShoutout = await db.AutoShoutouts.Where(x => x.Name.Equals(name.ToLower())).FirstOrDefaultAsync();
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
            await _serviceBackbone.SendChatMessage(message);

            await TwitchShoutOut(userId);
        }


        private async Task TwitchShoutOut(string userId)
        {

            if (LastShoutOut.AddMinutes(2) < DateTime.Now)
            {
                if (UserLastShoutout.ContainsKey(userId))
                {
                    if (UserLastShoutout[userId].AddMinutes(60) > DateTime.Now) return;
                }
                UserLastShoutout[userId] = DateTime.Now;
                if (_serviceBackbone.IsOnline == false) return;
                await _twitchService.ShoutoutStreamer(userId);
            }
        }

        private async Task UpdateLastShoutout(AutoShoutout? autoShoutout)
        {
            if (autoShoutout == null) return;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                autoShoutout.LastShoutout = DateTime.Now;
                db.AutoShoutouts.Update(autoShoutout);
                await db.SaveChangesAsync();
            }
        }

        public async Task<AutoShoutout?> GetAutoShoutoutAsync(int id)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                return await db.AutoShoutouts.Where(x => x.Id == id).FirstOrDefaultAsync();
            }
        }

        public async Task UpdateAutoShoutoutAsync(AutoShoutout autoShoutout)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.AutoShoutouts.Update(autoShoutout);
                await db.SaveChangesAsync();
            }
        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            switch (e.Command)
            {
                case "so":
                case "shoutout":
                    if (e.isMod == false && e.isBroadcaster == false && e.isVip == false)
                    {
                        return;
                    }
                    if (e.Args.Any() == false)
                    {
                        return;
                    }
                    await Shoutout(e.TargetUser);
                    break;
            }
        }
    }
}