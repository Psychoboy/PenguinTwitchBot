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
    public class UserFeature : BaseFeature
    {
        private Dictionary<string, DateTime> _usersLastActive = new Dictionary<string, DateTime>();
        private HashSet<string> _users = new HashSet<string>();
        private IViewerData _viewerData;
        private readonly ILogger<UserFeature> _logger;

        public UserFeature(
            ILogger<UserFeature> logger, 
            EventService eventService,
            IViewerData viewerData
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
            updateLastActive(e.Sender);
            return Task.CompletedTask;
        }

        public List<string> GetActiveUsers() {
            return _usersLastActive.Where(kvp => kvp.Value.AddMinutes(5) > DateTime.Now).Select(x => x.Key).ToList();
        }

        public List<string> GetCurrentUsers() {
            return _users.ToList();
        }

        public Viewer? GetViewer(string username) {
            var viewer = _viewerData.FindOne(username);
            return viewer;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.CompletedTask;
        }

        private Task OnSubscription(object? sender, SubscriptionEventArgs e)
        {
            updateLastActive(e.Sender);
            return Task.CompletedTask;
        }

        private Task OnCheer(object? sender, CheerEventArgs e)
        {
            updateLastActive(e.Sender);
            return Task.CompletedTask;
        }

        private void updateLastActive(string? sender) {
            if(sender == null) return;
            _usersLastActive[sender] = DateTime.Now;
            _logger.LogInformation("Updated active user: {0}", sender);
            _logger.LogInformation("Active Users: {0}", _usersLastActive.Count);
        }

        private Task OnChatMessage(object? sender, ChatMessageEventArgs e)
        {
            updateLastActive(e.Sender);
            var viewer = _viewerData.FindOne(e.Sender);
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
            _viewerData.InsertOrUpdate(viewer);

            return Task.CompletedTask;
        }
    }
}