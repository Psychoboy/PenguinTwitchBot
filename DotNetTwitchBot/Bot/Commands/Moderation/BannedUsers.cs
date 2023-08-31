using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Repository;
using DotNetTwitchBot.Bot.TwitchServices;
using Timer = System.Timers.Timer;

namespace DotNetTwitchBot.Bot.Commands.Moderation
{
    public class BannedUsers : BaseCommandService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Timer _timer = new(TimeSpan.FromHours(1).TotalMilliseconds);
        private readonly ITwitchService _twitchService;
        private readonly ILogger<BannedUsers> _logger;

        public BannedUsers(ILogger<BannedUsers> logger, IServiceScopeFactory scopeFactory, IServiceBackbone serviceBackbone, ICommandHandler commandHandler, ITwitchService twitchService) : base(serviceBackbone, commandHandler)
        {
            _twitchService = twitchService;
            _logger = logger;
            serviceBackbone.BanEvent += ServiceBackbone_BanEvent;
            _scopeFactory = scopeFactory;
            _timer.Elapsed += OnTimerElapsed;
            _timer.Start();
        }

        private async void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            await UpdateBannedUsers();
        }

        private async Task UpdateBannedUsers()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var bannedUsers = await db.BannedViewers.GetAllAsync();
            var curBannedUsers = await _twitchService.GetAllBannedViewers();
            foreach (var bannedUser in curBannedUsers)
            {
                var exists = await BanExists(bannedUser.UserLogin);
                if (exists == false)
                {
                    _logger.LogInformation("{0} didn't exist in banned user list adding...", bannedUser.UserLogin);
                    await AddBannedUser(bannedUser.UserLogin);
                }
            }

            foreach (var bannedUser in bannedUsers)
            {
                if (curBannedUsers.Exists(x => x.UserLogin.Equals(bannedUser.Username, StringComparison.OrdinalIgnoreCase)) == false)
                {
                    _logger.LogInformation("{0} shouldn't exist in banned user list removing...", bannedUser.Username);
                    await RemoveBannedUser(bannedUser.Username);
                }
            }
        }

        private async Task<bool> BanExists(string name)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var bannedUser = await db.BannedViewers.Find(x => x.Username.Equals(name)).FirstOrDefaultAsync();
            return bannedUser != null;
        }

        private async Task ServiceBackbone_BanEvent(object? sender, Events.BanEventArgs e)
        {

            if (e.IsUnBan)
            {
                await RemoveBannedUser(e.Name);
            }
            else
            {
                await AddBannedUser(e.Name);
            }
        }

        private async Task RemoveBannedUser(string name)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var bannedUser = await db.BannedViewers.Find(x => x.Username.Equals(name)).FirstOrDefaultAsync();
            if (bannedUser != null)
            {
                db.BannedViewers.Remove(bannedUser);

            }

            var viewerTime = await db.ViewersTime.Find(x => x.Username == name).FirstOrDefaultAsync();
            if (viewerTime != null)
            {
                viewerTime.banned = false;
                db.ViewersTime.Update(viewerTime);
            }

            var viewerTickets = await db.ViewerTickets.Find(x => x.Username == name).FirstOrDefaultAsync();
            if (viewerTickets != null)
            {
                viewerTickets.banned = false;
                db.ViewerTickets.Update(viewerTickets);
            }

            var viewerPasties = await db.ViewerPoints.Find(x => x.Username == name).FirstOrDefaultAsync();
            if (viewerPasties != null)
            {
                viewerPasties.banned = false;
                db.ViewerPoints.Update(viewerPasties);
            }

            var viewerMessages = await db.ViewerMessageCounts.Find(x => x.Username == name).FirstOrDefaultAsync();
            if (viewerMessages != null)
            {
                viewerMessages.banned = false;
                db.ViewerMessageCounts.Update(viewerMessages);
            }

            await db.SaveChangesAsync();
        }

        private async Task AddBannedUser(string name)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var bannedUser = await db.BannedViewers.Find(x => x.Username.Equals(name)).FirstOrDefaultAsync();
            if (bannedUser == null)
            {
                bannedUser = new BannedViewer { Username = name };
                db.BannedViewers.Add(bannedUser);

            }

            var viewerTime = await db.ViewersTime.Find(x => x.Username == name).FirstOrDefaultAsync();
            if (viewerTime != null)
            {
                viewerTime.banned = true;
                db.ViewersTime.Update(viewerTime);
            }

            var viewerTickets = await db.ViewerTickets.Find(x => x.Username == name).FirstOrDefaultAsync();
            if (viewerTickets != null)
            {
                viewerTickets.banned = true;
                db.ViewerTickets.Update(viewerTickets);
            }

            var viewerPasties = await db.ViewerPoints.Find(x => x.Username == name).FirstOrDefaultAsync();
            if (viewerPasties != null)
            {
                viewerPasties.banned = true;
                db.ViewerPoints.Update(viewerPasties);
            }

            var viewerMessages = await db.ViewerMessageCounts.Find(x => x.Username == name).FirstOrDefaultAsync();
            if (viewerMessages != null)
            {
                viewerMessages.banned = true;
                db.ViewerMessageCounts.Update(viewerMessages);
            }

            await db.SaveChangesAsync();
        }

        public override Task OnCommand(object? sender, CommandEventArgs e)
        {
            return Task.CompletedTask;
        }

        public override async Task Register()
        {
            await UpdateBannedUsers();
        }
    }
}
