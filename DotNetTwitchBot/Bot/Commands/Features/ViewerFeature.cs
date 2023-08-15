using System.Collections.Concurrent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Database;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Models;
using DotNetTwitchBot.Bot.TwitchServices;
using System.Timers;
using Timer = System.Timers.Timer;


namespace DotNetTwitchBot.Bot.Commands.Features
{
    public class ViewerFeature : BaseCommandService, IViewerFeature
    {
        private readonly ConcurrentDictionary<string, DateTime> _usersLastActive = new();
        private readonly ConcurrentDictionary<string, byte> _users = new();
        private readonly ConcurrentDictionary<string, DateTime> _lurkers = new();

        private readonly TwitchService _twitchService;
        private readonly ILogger<ViewerFeature> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Timer _timer = new(TimeSpan.FromHours(1).TotalMilliseconds);

        public ViewerFeature(
            ILogger<ViewerFeature> logger,
            IServiceBackbone eventService,
            TwitchService twitchService,
            IServiceScopeFactory scopeFactory,
            ICommandHandler commandHandler
            ) : base(eventService, commandHandler)
        {
            _logger = logger;
            eventService.ChatMessageEvent += OnChatMessage;
            eventService.SubscriptionEvent += OnSubscription;
            eventService.SubscriptionEndEvent += OnSubscriptionEnd;
            eventService.CheerEvent += OnCheer;
            eventService.FollowEvent += OnFollow;
            eventService.UserJoinedEvent += OnUserJoined;
            eventService.UserLeftEvent += OnUserLeft;
            _twitchService = twitchService;
            _scopeFactory = scopeFactory;

            _timer.Elapsed += OnTimerElapsed;
            _timer.Start();
        }

        public async Task<Viewer?> GetViewer(int id)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.Viewers.Where(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<Viewer>> GetViewers()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.Viewers.ToListAsync();
        }

        public async Task SaveViewer(Viewer viewer)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Viewers.Update(viewer);
            await db.SaveChangesAsync();
        }

        public async Task<List<Viewer>> SearchForViewer(string name)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.Viewers.Where(x => x.Username.Contains(name) || x.DisplayName.Contains(name)).ToListAsync();
        }

        private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {

            await UpdateSubscribers();
        }

        private async Task OnUserLeft(object? sender, UserLeftEventArgs e)
        {
            _users.Remove(e.Username, out _);
            await AddOrUpdateLastSeen(e.Username);
        }

        private async Task OnUserJoined(object? sender, UserJoinedEventArgs e)
        {
            _users[e.Username] = default;
            await AddOrUpdateLastSeen(e.Username);
        }

        private async Task AddOrUpdateLastSeen(string username)
        {
            var viewer = await GetViewer(username);
            if (viewer == null)
            {
                await AddBasicUser(username);
            }
            else
            {
                await UpdateLastSeen(viewer);
            }
        }



        private async Task UpdateLastSeen(Viewer viewer)
        {
            viewer.LastSeen = DateTime.Now;
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Viewers.Update(viewer);
            await db.SaveChangesAsync();
        }

        private async Task OnFollow(object? sender, FollowEventArgs e)
        {
            _logger.LogInformation("{0} Followed.", e.DisplayName);
            await ServiceBackbone.SendChatMessage($"Thank you for following {e.DisplayName} <3");
            UpdateLastActive(e.Username);
            await AddFollow(e);
        }

        private async Task AddFollow(FollowEventArgs args)
        {
            try
            {
                Follower? follower;
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    follower = await db.Followers.Where(x => x.Username.Equals(args.Username)).FirstOrDefaultAsync();
                }
                if (follower == null)
                {
                    follower = new Follower
                    {
                        Username = args.Username,
                        DisplayName = args.DisplayName,
                        FollowDate = args.FollowDate
                    };
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    await db.Followers.AddAsync(follower);
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding follow");
            }
        }

        public async Task<bool> IsFollower(string username)
        {
            var follower = await GetFollowerAsync(username);
            return follower != null;
        }

        public async Task<Follower?> GetFollowerAsync(string username)
        {
            Follower? follower;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                follower = await db.Followers.Where(x => x.Username.Equals(username)).FirstOrDefaultAsync();
            }
            if (follower != null)
            {
                return follower;
            }

            follower = await _twitchService.GetUserFollow(username);
            if (follower == null) return null;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await db.Followers.AddAsync(follower);
                await db.SaveChangesAsync();
            }
            return follower;
        }

        public List<string> GetActiveViewers()
        {
            var activeViewers = _usersLastActive.Where(kvp => kvp.Value.AddMinutes(15) > DateTime.Now).Select(x => x.Key).ToList();
            var lurkers = _lurkers.Where(x => x.Value > DateTime.Now.AddHours(-1)).Select(x => x.Key);
            activeViewers.AddRange(lurkers.Where(x => activeViewers.Contains(x) == false));
            return activeViewers;
        }

        public List<string> GetCurrentViewers()
        {
            var users = _users.Select(x => x.Key).ToList();
            var activeViewers = GetActiveViewers();
            users.AddRange(activeViewers.Where(x => users.Contains(x) == false));
            return users.ToList();
        }

        public async Task<Viewer?> GetViewer(string username)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.Viewers.FirstOrDefaultAsync(x => x.Username.Equals(username.ToLower()));
        }

        public async Task<string> GetDisplayName(string username)
        {
            var viewer = await GetViewer(username);
            return viewer != null ? viewer.DisplayName : username;
        }

        public async Task<string> GetNameWithTitle(string username)
        {
            var viewer = await GetViewer(username);
            return viewer != null ? viewer.NameWithTitle() : username;
        }

        public async Task<bool> IsSubscriber(string username)
        {
            var viewer = await GetViewer(username);
            if (viewer == null)
            {
                return false;
            }
            return viewer.isSub;
        }

        public async Task<bool> IsModerator(string username)
        {
            var viewer = await GetViewer(username);
            if (viewer == null)
            {
                return false;
            }
            return viewer.isMod;
        }

        private async Task OnSubscription(object? sender, SubscriptionEventArgs e)
        {
            if (e.Name == null) return;
            _logger.LogInformation("{0} Subscribed.", e.Name);
            await AddSubscription(e.Name);
            UpdateLastActive(e.Name);
        }

        private async Task OnSubscriptionEnd(object? sender, SubscriptionEndEventArgs e)
        {
            if (e.Name == null) return;
            _logger.LogInformation("{0} Unsubscribed", e.Name);
            await RemoveSubscription(e.Name);
        }

        private async Task AddSubscription(string username)
        {
            var viewer = await GetViewer(username);
            if (viewer == null) return;
            viewer.isSub = true;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Viewers.Update(viewer);
                await db.SaveChangesAsync();
            }
            _logger.LogInformation("{0} Subscription added.", username);
        }

        private async Task RemoveSubscription(string username)
        {
            var viewer = await GetViewer(username);
            if (viewer == null) return;
            viewer.isSub = false;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Viewers.Update(viewer);
                await db.SaveChangesAsync();
            }
            _logger.LogInformation("{0} Subscription removed.", username);
        }

        private Task OnCheer(object? sender, CheerEventArgs e)
        {
            if (!e.IsAnonymous)
            {
                UpdateLastActive(e.Name);
            }
            return Task.CompletedTask;
        }

        private void UpdateLastActive(string? sender)
        {
            if (sender == null) return;
            _usersLastActive[sender] = DateTime.Now;
        }

        private async Task OnChatMessage(object? sender, ChatMessageEventArgs e)
        {
            UpdateLastActive(e.Name);
            Viewer? viewer;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                viewer = await db.Viewers.Where(x => x.Username.Equals(e.Name)).FirstOrDefaultAsync();
            }
            viewer ??= new Viewer
            {
                DisplayName = e.DisplayName,
                Username = e.Name
            };
            if (viewer.DisplayName != e.DisplayName) viewer.DisplayName = e.DisplayName;
            if (viewer.isMod != e.IsMod) viewer.isMod = e.IsMod;
            if (viewer.isSub != e.IsSub) viewer.isSub = e.IsSub;
            if (viewer.isVip != e.IsVip) viewer.isVip = e.IsVip;
            if (viewer.isBroadcaster != e.IsBroadcaster) viewer.isBroadcaster = e.IsBroadcaster;
            viewer.LastSeen = DateTime.Now;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Update(viewer);
                await db.SaveChangesAsync();
            }
        }

        private async Task AddBasicUser(string name)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var viewer = await db.Viewers.Where(x => x.Username.Equals(name)).FirstOrDefaultAsync();
            if (viewer != null) return;
            viewer = new Viewer
            {
                DisplayName = name,
                Username = name,
                LastSeen = DateTime.Now
            };
            db.Update(viewer);
            await db.SaveChangesAsync();
        }

        private async Task UpdateSubscribers()
        {
            try
            {
                var subscribers = await _twitchService.GetAllSubscriptions();
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    foreach (var subscriber in subscribers)
                    {
                        var viewer = await GetViewer(subscriber.UserLogin);
                        viewer ??= new Viewer
                        {
                            Username = subscriber.UserLogin,
                            DisplayName = subscriber.UserName
                        };
                        if (viewer.isSub == false)
                        {
                            _logger.LogWarning("{0} was not a subscriber and is being updated manually bulk.", viewer.Username);
                        }
                        viewer.isSub = true;
                        db.Viewers.Update(viewer);
                    }
                    await db.SaveChangesAsync();
                }

                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var subTracker = scope.ServiceProvider.GetRequiredService<SubscriptionTracker>();
                    var missingNames = await subTracker.MissingSubs(subscribers.Select(x => x.UserLogin));
                    foreach (var missingName in missingNames)
                    {
                        await subTracker.AddOrUpdateSubHistory(missingName);
                    }
                }

                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var curSubscribers = await db.Viewers.Where(x => x.isSub == true).ToListAsync();
                    foreach (var curSubscriber in curSubscribers)
                    {
                        if (!subscribers.Exists(x => x.UserLogin.Equals(curSubscriber.Username)))
                        {
                            _logger.LogInformation("Removing Subscriber {0}", curSubscriber.Username);
                            curSubscriber.isSub = false;
                            db.Viewers.Update(curSubscriber);
                        }
                    }
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating subscribers");
            }
        }

        public override async Task Register()
        {
            var moduleName = "ViewerFeature";
            await RegisterDefaultCommand("lurk", this, moduleName);
            await UpdateSubscribers();
            _logger.LogInformation($"Registered {moduleName}");
        }

        public override Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return Task.CompletedTask;
            if (command.CommandProperties.CommandName.Equals("lurk"))
            {
                _lurkers[e.Name] = DateTime.Now;
            }
            return Task.CompletedTask;
        }


    }
}