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
        private readonly ILogger<ViewerFeature> _logger;

        public ViewerFeature(
            ILogger<ViewerFeature> logger, 
            EventService eventService,
            ViewerData viewerData
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

        private Task OnFollow(object? sender, FollowEventArgs e)
        {
            _logger.LogInformation("{0} Followed.", e.Sender);
            updateLastActive(e.Sender);
            return Task.CompletedTask;
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

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.CompletedTask;
        }

        private Task OnSubscription(object? sender, SubscriptionEventArgs e)
        {
            _logger.LogInformation("{0} Subscribed.", e.Sender);
            updateLastActive(e.Sender);
            return Task.CompletedTask;
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
            updateLastActive(e.Sender);
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
    }
}