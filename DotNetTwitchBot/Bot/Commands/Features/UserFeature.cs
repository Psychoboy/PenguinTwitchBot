using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;

namespace DotNetTwitchBot.Bot.Commands.Features
{
    public class UserFeature : BaseFeature
    {
        private Dictionary<string, DateTime> _usersLastActive = new Dictionary<string, DateTime>();
        private readonly ILogger<UserFeature> _logger;

        public UserFeature(ILogger<UserFeature> logger, EventService eventService) : base(eventService)
        {
            _logger = logger;
            eventService.ChatMessageEvent += OnChatMessage;
            eventService.SubscriptionEvent += OnSubscription;
            eventService.CheerEvent += OnCheer;
            eventService.FollowEvent += OnFollow;
        }

        private Task OnFollow(object? sender, FollowEventArgs e)
        {
            updateLastActive(e.Sender);
            return Task.CompletedTask;
        }

        public List<string> GetActiveUsers() {
            return _usersLastActive.Where(kvp => kvp.Value.AddMinutes(5) > DateTime.Now).Select(x => x.Key).ToList();
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

        private void updateLastActive(string sender) {
            _usersLastActive[sender] = DateTime.Now;
            _logger.LogInformation("Updated active user: {0}", sender);
            _logger.LogInformation("Active Users: {0}", _usersLastActive.Count);
        }

        private Task OnChatMessage(object? sender, ChatMessageEventArgs e)
        {
            updateLastActive(e.Sender);
            return Task.CompletedTask;
        }
    }
}