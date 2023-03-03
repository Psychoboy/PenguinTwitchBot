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
    public class GiveawayFeature : BaseFeature
    {
        private ILogger<GiveawayFeature> _logger;
        private GiveawayData _giveawayData;
        private PointsFeature _pointsFeature;
        private TwitchService _twitchService;

        public GiveawayFeature(
            ILogger<GiveawayFeature> logger,
            EventService eventService,
            GiveawayData giveawayData,
            PointsFeature pointsFeature,
            TwitchService twitchService
            ) : base(eventService)
        {
            _logger = logger;
            _giveawayData = giveawayData;
            _pointsFeature = pointsFeature;
            _twitchService = twitchService;
            eventService.CommandEvent += OnCommandEvent;

        }

        private async Task OnCommandEvent(object? sender, CommandEventArgs e)
        {
            switch(e.Command) {
                case "testenter":{
                    await Enter(e.Sender, e.Args.First());
                    break;
                }
                case "testentries":{
                    await Entries(e.Sender);
                    break;
                }
                case "testdraw": {
                    if(!e.isBroadcaster) return;
                    await Draw();
                    break;
                }
                case "testresetdraw": {
                    if(!e.isBroadcaster) return;
                    await Reset();
                    break;
                }
                case "testprize": {
                    break;
                }
            }
        }

        private async Task Reset()
        {
            await _giveawayData.DeleteAll();
        }

        private async Task Draw()
        {
            var entry = await _giveawayData.RandomEntry();
            if(entry == null) return;
            _logger.LogInformation("Entry Id: {0} Username {1} selected", entry.Id, entry.Username);
            var isFollower = await _twitchService.IsUserFollowing(entry.Username);
            await _eventService.SendChatMessage(string.Format("{0} won the drawing and {1} following", entry.Username, isFollower ? "is" : "is not"));

        }

        private async Task Enter(string sender, string amount) {
            amount = amount.ToLower();
            var viewerPoints = await _pointsFeature.GetViewerPoints(sender);

            if(amount == "max" || amount == "all") {
                var currentEntries = await _giveawayData.CountForUser(sender);
                if(currentEntries + viewerPoints > 1000000) // Max entries is 1million
                {
                    var maxPoints = 1000000 - currentEntries;
                    amount = maxPoints > 0 ? maxPoints.ToString() : "0";
                }
            }
            if(!Int64.TryParse(amount, out var points)) {
                await _eventService.SendChatMessage(string.Format("@{0}, please use a number or max/all when entering.", sender));
                return;
            }
            if(points == 0 || points > viewerPoints) {
                await _eventService.SendChatMessage(string.Format("@{0}, you do not have enough or that many tickets to enter.", sender));
                return;
            }
            if(!(await _pointsFeature.RemovePointsFromViewer(sender, points))) {
                await _eventService.SendChatMessage("@{0}, failed to enter giveaway. Please try again.");
                return;
            }

            if(points > 1000000) {
                await _eventService.SendChatMessage("@{0}, Max entries is 1,000,000");
            }

            
            var entries = new GiveawayEntry[points];
            for(var i = 0; i < points; i++) {
                entries[i] = new GiveawayEntry(){
                    Username = sender
                };
            }
            await _giveawayData.InsertAll(entries);
            
            
            await _eventService.SendChatMessage($"@{sender}, you have bought {points} entries.");
        }

        private async Task Entries(string sender) {
            var entries = await _giveawayData.CountForUser(sender);
            await _eventService.SendChatMessage($"@{sender}, you have {entries} entries.");
        }
    }
}