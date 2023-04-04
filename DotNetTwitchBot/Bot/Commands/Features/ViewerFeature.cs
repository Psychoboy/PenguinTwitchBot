using System.Collections.Concurrent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Database;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Models;
using DotNetTwitchBot.Bot.TwitchServices;
using System.Timers;
using Timer = System.Timers.Timer;


namespace DotNetTwitchBot.Bot.Commands.Features
{
    public class ViewerFeature : BaseCommand
    {
        private ConcurrentDictionary<string, DateTime> _usersLastActive = new ConcurrentDictionary<string, DateTime>();
        private ConcurrentDictionary<string, byte> _users = new ConcurrentDictionary<string, byte>();
        // private ApplicationDbContext _applicationDbContext;

        //private ViewerData _viewerData;
        // private FollowData _followData;
        private TwitchService _twitchService;
        private readonly TwitchBotService _twitchBotService;
        private readonly ILogger<ViewerFeature> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private Timer _timer;

        public ViewerFeature(
            ILogger<ViewerFeature> logger,
            ServiceBackbone eventService,
            // ViewerData viewerData,
            TwitchService twitchService,
            TwitchBotService twitchBotService,
            // FollowData followData
            // ApplicationDbContext applicationDbContext
            IServiceScopeFactory scopeFactory
            ) : base(eventService)
        {
            _logger = logger;
            eventService.ChatMessageEvent += OnChatMessage;
            eventService.SubscriptionEvent += OnSubscription;
            eventService.SubscriptionEndEvent += OnSubscriptionEnd;
            eventService.CheerEvent += OnCheer;
            eventService.FollowEvent += OnFollow;
            eventService.UserJoinedEvent += OnUserJoined;
            eventService.UserLeftEvent += OnUserLeft;

            // _viewerData = viewerData;
            // _followData = followData;
            // _applicationDbContext = services.GetRequiredService<ApplicationDbContext>();
            _twitchService = twitchService;
            _twitchBotService = twitchBotService;
            _scopeFactory = scopeFactory;
            _timer = new Timer(900000); //15 minutes
            _timer.Elapsed += OnTimerElapsed;
            _timer.Start();
        }

        private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {

            await UpdateSubscribers();
        }

        private async Task OnUserLeft(object? sender, UserLeftEventArgs e)
        {
            _users.Remove(e.Username, out byte doNotCare);
            await AddOrUpdateLastSeen(e.Username);
        }

        private async Task OnUserJoined(object? sender, UserJoinedEventArgs e)
        {
            _users[e.Username] = default(byte);
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
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Viewers.Update(viewer);
                await db.SaveChangesAsync();
            }
        }

        private async Task OnFollow(object? sender, FollowEventArgs e)
        {
            _logger.LogInformation("{0} Followed.", e.DisplayName);
            updateLastActive(e.Username);
            await AddFollow(e);
        }

        private async Task AddFollow(FollowEventArgs args)
        {
            // var follower = await _followData.GetFollower(args.Username);
            Follower? follower;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                follower = await db.Followers.Where(x => x.Username.Equals(args.Username)).FirstOrDefaultAsync();
            }
            if (follower == null)
            {
                follower = new Follower()
                {
                    Username = args.Username,
                    DisplayName = args.DisplayName,
                    FollowDate = args.FollowDate
                };
                //await _followData.Insert(follower);
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    await db.Followers.AddAsync(follower);
                    await db.SaveChangesAsync();
                }
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
            return _usersLastActive.Where(kvp => kvp.Value.AddMinutes(15) > DateTime.Now).Select(x => x.Key).ToList();
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
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                return await db.Viewers.FirstOrDefaultAsync(x => x.Username.Equals(username.ToLower()));
            }
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
            if (e.Sender == null) return;
            _logger.LogInformation("{0} Subscribed.", e.Sender);
            await AddSubscription(e.Sender);
            updateLastActive(e.Sender);
        }

        private async Task OnSubscriptionEnd(object? sender, SubscriptionEventArgs e)
        {
            if (e.Sender == null) return;
            _logger.LogInformation("{0} Unsubscribed", e.Sender);
            await RemoveSubscription(e.Sender);
            // updateLastActive(e.Sender);
        }

        private async Task AddSubscription(string username)
        {
            var viewer = await GetViewer(username);
            if (viewer == null) return;
            viewer.isSub = true;
            // await _viewerData.Update(viewer);\
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
                updateLastActive(e.Sender);
                _logger.LogInformation("{0} cheered {1} bits with message {2}", e.Sender, e.Amount, e.Message);
            }
            else
            {
                _logger.LogInformation("Anonymous User cheered {0} bits", e.Amount);
            }
            return Task.CompletedTask;
        }

        private void updateLastActive(string? sender)
        {
            if (sender == null) return;
            _usersLastActive[sender] = DateTime.Now;
        }

        private async Task OnChatMessage(object? sender, ChatMessageEventArgs e)
        {
            updateLastActive(e.Sender);
            //var viewer = await _viewerData.FindOne(e.Sender);
            Viewer? viewer;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                viewer = await db.Viewers.Where(x => x.Username.Equals(e.Sender)).FirstOrDefaultAsync();
            }
            if (viewer == null)
            {
                viewer = new Models.Viewer()
                {
                    DisplayName = e.DisplayName,
                    Username = e.Sender
                };
            }
            if (viewer.DisplayName != e.DisplayName) viewer.DisplayName = e.DisplayName;
            if (viewer.isMod != e.isMod) viewer.isMod = e.isMod;
            if (viewer.isSub != e.isSub) viewer.isSub = e.isSub;
            if (viewer.isVip != e.isVip) viewer.isVip = e.isVip;
            if (viewer.isBroadcaster != e.isBroadcaster) viewer.isBroadcaster = e.isBroadcaster;
            viewer.LastSeen = DateTime.Now;
            //await _viewerData.InsertOrUpdate(viewer);
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Update(viewer);
                await db.SaveChangesAsync();
            }
        }

        private async Task AddBasicUser(string name)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
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
        }

        public async Task UpdateSubscribers()
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
                        if (viewer == null)
                        {
                            viewer = new Viewer()
                            {
                                Username = subscriber.UserLogin,
                                DisplayName = subscriber.UserName
                            };
                        }
                        viewer.isSub = true;
                        db.Viewers.Update(viewer);
                    }
                    await db.SaveChangesAsync();
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

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            if (e.Command.Equals("tw"))
            {
                await _twitchBotService.SendWhisper(e.Name, "Test Whisper Message");
            }
            // return Task.CompletedTask;
        }
    }
}