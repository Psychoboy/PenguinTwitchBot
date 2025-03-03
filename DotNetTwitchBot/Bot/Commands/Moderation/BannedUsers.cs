using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.TwitchServices;
using DotNetTwitchBot.Repository;
using Timer = System.Timers.Timer;

namespace DotNetTwitchBot.Bot.Commands.Moderation
{
    public class BannedUsers : BaseCommandService, IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Timer _timer = new(TimeSpan.FromHours(1).TotalMilliseconds);
        private readonly ITwitchService _twitchService;
        private readonly ILogger<BannedUsers> _logger;

        public BannedUsers(ILogger<BannedUsers> logger, IServiceScopeFactory scopeFactory, IServiceBackbone serviceBackbone, ICommandHandler commandHandler, ITwitchService twitchService) : base(serviceBackbone, commandHandler, "BannedUsers")
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
            try
            {
                await UpdateBannedUsers();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating banned users.");
            }
        }

        private async Task UpdateBannedUsers()
        {
            try
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
                        if (bannedUser.ExpiresAt.HasValue == false) continue;
                        _logger.LogInformation("{user} didn't exist in banned user list adding...", bannedUser.UserLogin);
                        await AddBannedUser(new Events.BanEventArgs
                        {
                            Name = bannedUser.UserLogin,
                            BanEndsAt = bannedUser.ExpiresAt,
                            UserId = bannedUser.UserId
                        });
                    }
                }

                foreach (var bannedUser in bannedUsers)
                {
                    if (curBannedUsers.Exists(x => x.UserLogin.Equals(bannedUser.Username, StringComparison.OrdinalIgnoreCase)) == false)
                    {
                        _logger.LogInformation("{user} shouldn't exist in banned user list removing...", bannedUser.Username);
                        await RemoveBannedUser(bannedUser.Username);
                    }
                }
            }
            catch (Exception)
            {
                _logger.LogCritical("Failed getting banned users.");
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
                await AddBannedUser(e);
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

            var viewerMessages = await db.ViewerMessageCounts.Find(x => x.Username == name).FirstOrDefaultAsync();
            if (viewerMessages != null)
            {
                viewerMessages.banned = false;
                db.ViewerMessageCounts.Update(viewerMessages);
            }

            var userPoints = await db.UserPoints.Find(x => x.Username.Equals(name)).ToListAsync();
            foreach (var userPoint in userPoints)
            {
                userPoint.Banned = false;
                db.UserPoints.Update(userPoint);
            }

            await db.SaveChangesAsync();
        }

        private async Task AddBannedUser(Events.BanEventArgs e)
        {
            var name = e.Name;
            if (e.BanEndsAt != null) return;
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

            var viewerMessages = await db.ViewerMessageCounts.Find(x => x.Username == name).FirstOrDefaultAsync();
            if (viewerMessages != null)
            {
                viewerMessages.banned = true;
                db.ViewerMessageCounts.Update(viewerMessages);
            }

            var userPoints = await db.UserPoints.Find(x => x.Username.Equals(name)).ToListAsync();
            foreach(var userPoint in userPoints)
            {
                userPoint.Banned = true;
                db.UserPoints.Update(userPoint);
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
