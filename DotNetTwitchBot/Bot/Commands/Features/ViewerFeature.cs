using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models;
using DotNetTwitchBot.Bot.TwitchServices;
using DotNetTwitchBot.Repository;
using System.Collections.Concurrent;
using System.Timers;
using TwitchLib.Api.Helix.Models.Teams;
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
                    await AddOrUpdateLastSeen(chatter.UserId, chatter.UserLogin);
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


        private async Task AddOrUpdateLastSeen(string userId, string userLogin)
        {
            var viewer = await GetViewerByUserIdOrName(userId, userLogin);
            if (viewer == null)
            {
                await AddBasicUser(userId, userLogin);
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
                        UserId = args.UserId,
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

        public async Task<DateTime?> GetUserCreatedAsync(string username)
        {
            DateTime? dateCreated = null;
            try
            {
                var user = await _twitchService.GetUser(username);
                if (user != null)
                {
                    dateCreated = user.CreatedAt;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting user");
            }
            return dateCreated;
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

        public async Task<Viewer?> GetViewerByUserId(string userId)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.Viewers.Find(x => x.UserId.Equals(userId)).FirstOrDefaultAsync();
        }

        public async Task<Viewer?> GetViewerByUserIdOrName(string userId, string username)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var viewer =  await db.Viewers.Find(x => x.UserId.Equals(userId)).FirstOrDefaultAsync();
            if(viewer == null)
            {
                viewer = await db.Viewers.Find(x => x.Username.Equals(username.ToLower())).FirstOrDefaultAsync();
            }
            return viewer;
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
            Viewer? viewer = await GetViewerByUserIdOrName(e.UserId, e.Name);

            viewer ??= new Viewer
            {
                UserId = e.UserId
            };
            if (viewer.DisplayName != e.DisplayName) viewer.DisplayName = e.DisplayName;
            if (viewer.Username != e.Name) viewer.Username = e.Name;
            if (viewer.isMod != e.IsMod) viewer.isMod = e.IsMod;
            if (viewer.isSub != e.IsSub) viewer.isSub = e.IsSub;
            if (viewer.isVip != e.IsVip) viewer.isVip = e.IsVip;
            if (viewer.isBroadcaster != e.IsBroadcaster) viewer.isBroadcaster = e.IsBroadcaster;
            viewer.LastSeen = DateTime.Now;
            await UpdateViewer(viewer);
        }

        private async Task AddBasicUser(string userId, string userLogin)
        {
            var viewer = await GetViewerByUserIdOrName(userId, userLogin);
            if (viewer != null) return;
            viewer = new Viewer
            {
                DisplayName = userLogin,
                UserId = userId,
                Username = userLogin,
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
                    foreach (var subscriber in subscribers.DistinctBy(x => x.UserLogin))
                    {
                        try
                        {
                            var viewer = await GetViewerByUserIdOrName(subscriber.UserId,subscriber.UserLogin);
                            viewer ??= new Viewer
                            {
                                UserId = subscriber.UserId
                            };

                            if (viewer.isSub == false)
                            {
                                _logger.LogWarning("{name} was not a subscriber and is being updated manually bulk.", viewer.Username);
                            }
                            viewer.Username = subscriber.UserLogin;
                            viewer.DisplayName = subscriber.UserName;
                            viewer.isSub = true;
                            db.Viewers.Update(viewer);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed updating subscription for {userlogin}", subscriber.UserLogin);
                        }
                    }
                    await db.SaveChangesAsync();
                }

                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var subTracker = scope.ServiceProvider.GetRequiredService<SubscriptionTracker>();
                    var missingNames = await subTracker.MissingSubs(subscribers.Select(x => x.UserLogin));
                    foreach (var missingName in missingNames)
                    {
                        var viewer = await _twitchService.GetUser(missingName);
                        if(viewer == null)
                        {
                            _logger.LogWarning("Viewer doesn't exist: {name}", missingName);
                            continue;
                        }
                        await subTracker.AddOrUpdateSubHistory(missingName, viewer.Id);
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
            await UpdateViewerIds();
            await UpdateSubscribers();
            await UpdateChatters();
            await Register();
        }

        private async Task UpdateViewerIds()
        {
            _logger.LogInformation("Updating viewer Ids");
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            _logger.LogInformation("Upgrading Viewers");
            //var viewers = db.Viewers.Get(x => x.UserId.Equals("")).ToList();
            //while (viewers.Any())
            //{
            //    _logger.LogInformation("{num} records to process.", viewers.Count);
            //    var procViewers = viewers.Take(100);
            //    if (viewers.Count > 100)
            //    {
            //        viewers.RemoveRange(0, 100);
            //    } else
            //    {
            //        viewers.Clear();
            //    }
            //    var users = await _twitchService.GetUsers(procViewers.Select(x => x.Username).ToList());
            //    if(users == null)
            //    {
            //        users = await _twitchService.GetUsers(procViewers.Select(x => x.Username).ToList());
            //    }
            //    if(users == null)
            //    {
            //        _logger.LogWarning("Received null...");
            //        continue;
            //    }
            //    foreach (var user in users) 
            //    { 
            //        var dbUser = procViewers.Where(x => x.Username.Equals(user.Login, StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();
            //        if (dbUser != null)
            //        {
            //            dbUser.UserId = user.Id;
            //            db.Viewers.Update(dbUser);
            //        }
            //    }
            //}

            _logger.LogInformation("Upgrading Viewers");
            var viewers = db.Viewers.Get(x => x.UserId.Equals("")).ToList();
            foreach (var viewer in viewers)
            {
                var tViewer = await _twitchService.GetUser(viewer.Username);
                if (tViewer == null)
                {
                    _logger.LogWarning("No viewer exists with name: {name}", viewer.Username);
                    continue;
                }
                viewer.UserId = tViewer.Id;
                db.Viewers.Update(viewer);
            }

            _logger.LogInformation("Upgrading Follows");
            var follows = db.Followers.GetAll();
            foreach (var follow in follows)
            {
                var tViewer = await _twitchService.GetUser(follow.Username);
                if (tViewer == null)
                {
                    _logger.LogWarning("No viewer exists with name: {name}", follow.Username);
                    continue;
                }
                follow.UserId = tViewer.Id;
                db.Followers.Update(follow);
            }

            _logger.LogInformation("Upgrading Subscription Histories");
            var subscriptions = db.SubscriptionHistories.GetAll();
            foreach (var sub in subscriptions)
            {
                var tViewer = await _twitchService.GetUser(sub.Username);
                if (tViewer == null)
                {
                    _logger.LogWarning("No viewer exists with name: {name}", sub.Username);
                    continue;
                }
                sub.UserId = tViewer.Id;
                db.SubscriptionHistories.Update(sub);
            }

            _logger.LogInformation("Upgrading Message Count");
            var messageCounts = db.ViewerMessageCounts.GetAll();
            foreach (var count in messageCounts)
            {
                var tViewer = await _twitchService.GetUser(count.Username);
                if (tViewer == null)
                {
                    _logger.LogWarning("No viewer exists with name: {name}", count.Username);
                    continue;
                }
                count.UserId = tViewer.Id;
                db.ViewerMessageCounts.Update(count);
            }

            _logger.LogInformation("Upgrading Viewer Points");
            var viewerPoints = db.ViewerPoints.GetAll();
            foreach (var viewerPoint in viewerPoints)
            {
                var tViewer = await _twitchService.GetUser(viewerPoint.Username);
                if (tViewer == null)
                {
                    _logger.LogWarning("No viewer exists with name: {name}", viewerPoint.Username);
                    continue;
                }
                viewerPoint.UserId = tViewer.Id;
                db.ViewerPoints.Update(viewerPoint);
            }

            _logger.LogInformation("Upgrading Viewer Tickets");
            var viewerTickets = db.ViewerTickets.GetAll();
            foreach (var viewerTicket in viewerTickets)
            {
                var tViewer = await _twitchService.GetUser(viewerTicket.Username);
                if (tViewer == null)
                {
                    _logger.LogWarning("No viewer exists with name: {name}", viewerTicket.Username);
                    continue;
                }
                viewerTicket.UserId = tViewer.Id;
                db.ViewerTickets.Update(viewerTicket);
            }

            _logger.LogInformation("Upgrading Viewer Time");
            var viewerTimes = db.ViewersTime.GetAll();
            foreach (var viewerTime in viewerTimes)
            {
                var tViewer = await _twitchService.GetUser(viewerTime.Username);
                if (tViewer == null)
                {
                    _logger.LogWarning("No viewer exists with name: {name}", viewerTime.Username);
                    continue;
                }
                viewerTime.UserId = tViewer.Id;
                db.ViewersTime.Update(viewerTime);
            }
            var result = await db.SaveChangesAsync();
            _logger.LogInformation("Finished updating. Updated {number} records", result);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopped {moduledname}", ModuleName);
            return Task.CompletedTask;
        }
    }
}