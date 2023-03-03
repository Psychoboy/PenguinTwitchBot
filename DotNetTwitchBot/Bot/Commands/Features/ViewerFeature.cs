using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Database;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Models;

namespace DotNetTwitchBot.Bot.Commands.Features
{
    public class ViewerFeature : BaseFeature
    {
        private Dictionary<string, DateTime> _usersLastActive = new Dictionary<string, DateTime>();
        private HashSet<string> _users = new HashSet<string>();
        private ViewerData _viewerData;
        private FollowData _followData;
        private TwitchService _twitchService;
        private readonly ILogger<ViewerFeature> _logger;

        public ViewerFeature(
            ILogger<ViewerFeature> logger, 
            EventService eventService,
            ViewerData viewerData,
            TwitchService twitchService,
            FollowData followData
            ) : base(eventService)
        {
            _logger = logger;
            eventService.ChatMessageEvent += OnChatMessage;
            eventService.SubscriptionEvent += OnSubscription;
            eventService.CheerEvent += OnCheer;
            eventService.FollowEvent += OnFollow;
            eventService.UserJoinedEvent += OnUserJoined;
            eventService.UserLeftEvent += OnUserLeft;

            _viewerData = viewerData;
            _followData = followData;
            _twitchService = twitchService;
        }

        private Task OnUserLeft(object? sender, UserLeftEventArgs e)
        {
            _users.Remove(e.Username);
            return Task.CompletedTask;
        }

        private Task OnUserJoined(object? sender, UserJoinedEventArgs e)
        {
            _users.Add(e.Username);
            return Task.CompletedTask;
        }

        private async Task OnFollow(object? sender, FollowEventArgs e)
        {
            _logger.LogInformation("{0} Followed.", e.DisplayName);
            updateLastActive(e.Username);
            await AddFollow(e);
        }

        private async Task AddFollow(FollowEventArgs args) {
            var follower = await _followData.GetFollower(args.Username);
            if(follower == null) {
                follower = new Follower(){
                    Username = args.Username,
                    DisplayName = args.DisplayName,
                    FollowDate = args.FollowDate
                };
                await _followData.Insert(follower); 
            }
        }

        public async Task<bool> IsFollower(string username) {
            var follower = await _followData.GetFollower(username);
            if(follower != null) {
                return true;
            }

            follower = await _twitchService.GetUserFollow(username);
            if(follower == null) return false;
            await _followData.Insert(follower);
            return true;
        }

        public List<string> GetActiveViewers() {
            return _usersLastActive.Where(kvp => kvp.Value.AddMinutes(5) > DateTime.Now).Select(x => x.Key).ToList();
        }

        public List<string> GetCurrentViewers() {
            return _users.ToList();
        }

        public async Task<Viewer?> GetViewer(string username) {
            return await _viewerData.FindOne(username);
        }

        private async Task OnSubscription(object? sender, SubscriptionEventArgs e)
        {
            if(e.Sender == null) return;
            _logger.LogInformation("{0} Subscribed.", e.Sender);
            await AddSubscription(e.Sender);
            updateLastActive(e.Sender);
        }

        private async Task AddSubscription(string username) {
            var viewer = await GetViewer(username);
            if(viewer == null) return;
            viewer.isSub = true;
            await _viewerData.Update(viewer);
            _logger.LogInformation("{0} Subscription added.", username);
        }

        private async Task RemoveSubscription(string username) {
            var viewer = await GetViewer(username);
            if(viewer == null) return;
            viewer.isSub = false;
            await _viewerData.Update(viewer);
            _logger.LogInformation("{0} Subscription removed.", username);
        }

        private Task OnCheer(object? sender, CheerEventArgs e)
        {
            if(!e.IsAnonymous){
                updateLastActive(e.Sender);
                _logger.LogInformation("{0} cheered {1} bits with message {2}", e.Sender, e.Amount, e.Message);
            } else {
                _logger.LogInformation("Anonymous User cheered {0} bits", e.Amount);
            }
            return Task.CompletedTask;
        }

        private void updateLastActive(string? sender) {
            if(sender == null) return;
            _usersLastActive[sender] = DateTime.Now;
        }

        private async Task OnChatMessage(object? sender, ChatMessageEventArgs e)
        {
            updateLastActive(e.Sender);
            var viewer = await _viewerData.FindOne(e.Sender);
            if(viewer == null) {
                viewer = new Models.Viewer(){
                    DisplayName = e.DisplayName,
                    Username = e.Sender
                };
            }

            viewer.isMod = e.isMod;
            viewer.isSub = e.isSub;
            viewer.isVip = e.isVip;
            viewer.isBroadcaster = e.isBroadcaster;
            viewer.LastSeen = DateTime.Now;
            await _viewerData.InsertOrUpdate(viewer);
        }

        public async Task LoadSubscribers(){
            _logger.LogInformation("Loading Subscribers");
            var subscribers = await _twitchService.GetAllSubscriptions();
            foreach(var subscriber in subscribers){
                var viewer = await GetViewer(subscriber.UserLogin);
                if(viewer == null) {
                    viewer = new Viewer(){
                        Username = subscriber.UserLogin,
                        DisplayName = subscriber.UserName
                    };
                }
                viewer.isSub = true;
                await _viewerData.InsertOrUpdate(viewer);
            }
            _logger.LogInformation("Getting existing subscribers.");
            var curSubscribers = await _viewerData.GetAllSubscribers();
            foreach(var curSubscriber in curSubscribers) {
                if(!subscribers.Exists(x => x.UserLogin.Equals(curSubscriber.Username))) {
                    _logger.LogInformation("Removing Subscriber {0}", curSubscriber.Username);
                    curSubscriber.isSub = false;
                    await _viewerData.Update(curSubscriber);
                }
            }

            _logger.LogInformation("Done updating subscribers, Total: {0}", subscribers.Count);
        }
    }
}