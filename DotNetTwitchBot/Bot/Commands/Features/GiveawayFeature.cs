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
        private IGiveawayEntries _giveawayEntries;
        private IDbViewerPoints _viewerPoints;

        public GiveawayFeature(
            EventService eventService,
            IGiveawayEntries giveawayEntries,
            IDbViewerPoints viewerPoints
            ) : base(eventService)
        {
            _giveawayEntries = giveawayEntries;
            _viewerPoints = viewerPoints;
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
            var viewerPoints = _viewerPoints.FindOne(sender);
            if(viewerPoints == null) {
                await _eventService.SendChatMessage(string.Format("@{0}, you do not have any tickets to enter.", sender));
                return;
            }
            if(amount == "max" || amount == "all") {
                amount = viewerPoints.Points.ToString();
            }
            if(!Int32.TryParse(amount, out var points)) {
                await _eventService.SendChatMessage(string.Format("@{0}, please use a number or max/all when entering.", sender));
                return;
            }
            if(points == 0 || points > viewerPoints.Points) {
                await _eventService.SendChatMessage(string.Format("@{0}, you do not have enough or that many tickets to enter.", sender));
                return;
            }

            viewerPoints.Points -= points;
            _viewerPoints.Update(viewerPoints);
            var entries = new GiveawayEntry[points];
            for(var i = 0; i < points; i++) {
                entries[i] = new GiveawayEntry(){
                    Username = sender
                };
            }
            _giveawayEntries.InsertBulk(entries);
            await _eventService.SendChatMessage($"@{sender}, you have bought {points} entries.");
        }

        private async Task Entries(string sender) {
            var entries = _giveawayEntries.Count(sender);
            await _eventService.SendChatMessage($"@{sender}, you have {entries} entries.");
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.CompletedTask;
        }
    }
}