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
    public class GiveawayFeature : BaseCommand
    {
        private ILogger<GiveawayFeature> _logger;
        private GiveawayData _giveawayData;
        private TicketsFeature _ticketsFeature;
        private ViewerFeature _viewerFeature;

        public GiveawayFeature(
            ILogger<GiveawayFeature> logger,
            ServiceBackbone eventService,
            GiveawayData giveawayData,
            TicketsFeature ticketsFeature,
            ViewerFeature viewerFeature
            ) : base(eventService)
        {
            _logger = logger;
            _giveawayData = giveawayData;
            _ticketsFeature = ticketsFeature;
            _viewerFeature = viewerFeature;

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
            var isFollower = await _viewerFeature.IsFollower(entry.Username);
            await _eventService.SendChatMessage(string.Format("{0} won the drawing and {1} following", entry.Username, isFollower ? "is" : "is not"));

        }

        

        private async Task Enter(string sender, string amount) {
            amount = amount.ToLower();
            var viewerPoints = await _ticketsFeature.GetViewerTickets(sender);

            if(amount == "max" || amount == "all") {
                amount = (await _ticketsFeature.GetViewerTickets(sender)).ToString();
            }
            if(!Int64.TryParse(amount, out var points)) {
                await _eventService.SendChatMessage(string.Format("@{0}, please use a number or max/all when entering.", sender));
                return;
            }
            if(points == 0 || points > viewerPoints) {
                await _eventService.SendChatMessage(string.Format("@{0}, you do not have enough or that many tickets to enter.", sender));
                return;
            }

            if(points < 0) {await _eventService.SendChatMessage(string.Format("@{0}, don't be dumb.", sender));}

            if(!(await _ticketsFeature.RemoveTicketsFromViewer(sender, points))) {
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

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            switch(e.Command) {
                case "testenter":{
                    if(e.Args.Count() == 0) return;
                    await Enter(e.Name, e.Args.First());
                    break;
                }
                case "testentries":{
                    await Entries(e.Name);
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

    }
}