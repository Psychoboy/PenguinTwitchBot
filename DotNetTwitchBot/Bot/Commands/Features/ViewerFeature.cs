using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.TwitchServices;
using DotNetTwitchBot.Repository;
using System.Collections.Concurrent;
using System.Timers;
using Timer = System.Timers.Timer;

namespace DotNetTwitchBot.Bot.Commands.Features
{
    public class ViewerFeature : BaseCommandService, IHostedService, IViewerFeature
    {
        private readonly ConcurrentDictionary<string, DateTime> _usersLastActive = new();
        private ConcurrentBag<string> _users = new();
        private readonly ConcurrentDictionary<string, DateTime> _lurkers = new();

        private readonly ITwitchService _twitchService;
        private readonly ILogger<ViewerFeature> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Timer _subscriberUpdateTimer = new(TimeSpan.FromHours(1).TotalMilliseconds);
        private readonly Timer _chatterUpdateTimer = new(TimeSpan.FromMinutes(2).TotalMilliseconds);
        private static readonly Prometheus.Gauge ActiveViewers = Prometheus.Metrics.CreateGauge("active_viewers", "Current active viewers in chat");
        private static readonly Prometheus.Gauge CurrentViewers = Prometheus.Metrics.CreateGauge("current_viewers", "Current viewers in chat");

        public ViewerFeature(
            ILogger<ViewerFeature> logger,
            IServiceBackbone serviceBackbone,
            ITwitchService twitchService,
            IServiceScopeFactory scopeFactory,
            ICommandHandler commandHandler
            ) : base(serviceBackbone, commandHandler, "ViewerFeature")
        {
            _logger = logger;

            serviceBackbone.SubscriptionEvent += OnSubscription;
            serviceBackbone.SubscriptionEndEvent += OnSubscriptionEnd;
            serviceBackbone.CheerEvent += OnCheer;
            serviceBackbone.FollowEvent += OnFollow;

            _twitchService = twitchService;
            _scopeFactory = scopeFactory;

            _subscriberUpdateTimer.Elapsed += OnSubscriberTimerElapsed;
            _subscriberUpdateTimer.Start();
            _chatterUpdateTimer.Elapsed += OnChatterUpdaterTimerElapsed;
            _chatterUpdateTimer.Start();
            Prometheus.Metrics.DefaultRegistry.AddBeforeCollectCallback(() =>
            {
                GetCurrentViewers();
            });
        }

        private async void OnChatterUpdaterTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            await UpdateChatters();
        }

        private async Task UpdateChatters()
        {
            try
            {
                var chatters = await _twitchService.GetCurrentChatters();
                _users = new(chatters.Select(x => x.UserLogin).Distinct());
                foreach (var chatter in chatters)
                {
                    await AddOrUpdateLastSeen(chatter.UserLogin);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating chatters.");
            }
        }

        public async Task<Viewer?> GetViewer(int id)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.Viewers.Find(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task SaveViewer(Viewer viewer)
        {
            await UpdateViewer(viewer);
        }

        public async Task<List<Viewer>> SearchForViewer(string name)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.Viewers.Find(x => x.Username.Contains(name) || x.DisplayName.Contains(name)).ToListAsync();
        }

        private async void OnSubscriberTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            await UpdateSubscribers();
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

        private async Task UpdateViewer(Viewer viewer)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            if (viewer.Id == null)
            {
                await db.Viewers.AddAsync(viewer);
            }
            else
            {

                db.Viewers.Update(viewer);
            }
            await db.SaveChangesAsync();
        }

        private async Task UpdateLastSeen(Viewer viewer)
        {
            viewer.LastSeen = DateTime.Now;
            await UpdateViewer(viewer);
        }

        private async Task OnFollow(object? sender, FollowEventArgs e)
        {
            _logger.LogInformation("{DisplayName} Followed.", e.DisplayName);
            await ServiceBackbone.SendChatMessage($"Thank you for following {e.DisplayName} <3");
            UpdateLastActive(e.Username);
            await AddFollow(e);
        }

        private async Task<Follower?> GetFollower(string username)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.Followers.Find(x => x.Username.Equals(username)).FirstOrDefaultAsync();

        }

        private async Task AddFollow(FollowEventArgs args)
        {
            try
            {
                Follower? follower = await GetFollower(args.Username);
                if (follower == null)
                {
                    follower = new Follower
                    {
                        Username = args.Username,
                        DisplayName = args.DisplayName,
                        FollowDate = args.FollowDate
                    };
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
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
            Follower? follower = await GetFollower(username);
            if (follower != null)
            {
                return follower;
            }

            follower = await _twitchService.GetUserFollow(username);
            if (follower == null) return null;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                await db.Followers.AddAsync(follower);
                await db.SaveChangesAsync();
            }
            return follower;
        }

        public List<string> GetActiveViewers()
        {
            var activeViewers = _usersLastActive.Where(kvp => kvp.Value.AddMinutes(15) > DateTime.Now).Select(x => x.Key).ToList();
            ActiveViewers.Set(activeViewers.Count);
            var lurkers = _lurkers.Where(x => x.Value > DateTime.Now.AddHours(-1)).Select(x => x.Key);
            activeViewers.AddRange(lurkers.Where(x => activeViewers.Contains(x) == false));
            return activeViewers;
        }

        public List<string> GetCurrentViewers()
        {
            var users = _users.ToList();
            CurrentViewers.Set(users.Count);
            var activeViewers = GetActiveViewers();
            users.AddRange(activeViewers.Where(x => users.Contains(x) == false));
            return [.. users];
        }

        public async Task<Viewer?> GetViewer(string username)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.Viewers.Find(x => x.Username.Equals(username.ToLower())).FirstOrDefaultAsync();
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
            _logger.LogInformation("{name} Subscribed.", e.Name);
            await AddSubscription(e.Name);
            UpdateLastActive(e.Name);
        }

        private async Task OnSubscriptionEnd(object? sender, SubscriptionEndEventArgs e)
        {
            if (e.Name == null) return;
            _logger.LogInformation("{name} Unsubscribed", e.Name);
            await RemoveSubscription(e.Name);
        }

        private async Task AddSubscription(string username)
        {
            var viewer = await GetViewer(username);
            if (viewer == null) return;
            viewer.isSub = true;
            await UpdateViewer(viewer);
            _logger.LogInformation("{name} Subscription added.", username);
        }

        private async Task RemoveSubscription(string username)
        {
            var viewer = await GetViewer(username);
            if (viewer == null) return;
            viewer.isSub = false;
            await UpdateViewer(viewer);
            _logger.LogInformation("{name} Subscription removed.", username);
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

        public async Task OnChatMessage(ChatMessageEventArgs e)
        {
            UpdateLastActive(e.Name);
            Viewer? viewer = await GetViewer(e.Name);

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
            await UpdateViewer(viewer);
        }

        private async Task AddBasicUser(string name)
        {
            var viewer = await GetViewer(name);
            if (viewer != null) return;
            viewer = new Viewer
            {
                DisplayName = name,
                Username = name,
                LastSeen = DateTime.Now
            };
            await UpdateViewer(viewer);
        }

        private async Task UpdateSubscribers()
        {
            try
            {
                var subscribers = await _twitchService.GetAllSubscriptions();
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
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
                            _logger.LogWarning("{name} was not a subscriber and is being updated manually bulk.", viewer.Username);
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
                    var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var curSubscribers = await db.Viewers.Find(x => x.isSub == true).ToListAsync();
                    foreach (var curSubscriber in curSubscribers)
                    {
                        if (!subscribers.Exists(x => x.UserLogin.Equals(curSubscriber.Username)))
                        {
                            _logger.LogInformation("Removing Subscriber {name}", curSubscriber.Username);
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
            _logger.LogInformation("Registered {moduleName}", moduleName);
        }

        public override Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommandDefaultName(e.Command);
            if (command.Equals("lurk"))
            {
                _lurkers[e.Name] = DateTime.Now;
            }
            return Task.CompletedTask;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await UpdateSubscribers();
            await UpdateChatters();
            await Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopped {moduledname}", ModuleName);
            return Task.CompletedTask;
        }
    }
}