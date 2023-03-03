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
        private GiveawayData _giveawayData;
        private PointsFeature _pointsFeature;

        public GiveawayFeature(
            EventService eventService,
            GiveawayData giveawayData,
            PointsFeature pointsFeature
            ) : base(eventService)
        {
            _giveawayData = giveawayData;
            _pointsFeature = pointsFeature;
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
                    break;
                }
                case "testresetdraw": {
                    break;
                }
                case "testprize": {
                    break;
                }
            }
        }

        private async Task Enter(string sender, string amount) {
            amount = amount.ToLower();
            var viewerPoints = await _pointsFeature.GetViewerPoints(sender);

            if(amount == "max" || amount == "all") {
                amount = viewerPoints.ToString();
            }
            if(!Int32.TryParse(amount, out var points)) {
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