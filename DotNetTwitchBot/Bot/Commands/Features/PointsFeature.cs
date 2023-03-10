using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;
using System.Timers;
using Timer = System.Timers.Timer;
using DotNetTwitchBot.Bot.Core.Database;

namespace DotNetTwitchBot.Bot.Commands.Features
{
    public class TicketsFeature : BaseCommand
    {
        private readonly ILogger<TicketsFeature> _logger;
        
        Timer _autoPointsTimer;
        
        private ViewerFeature _viewerFeature;
        private TicketsData _ticketsData;

        

        public TicketsFeature(
            ILogger<TicketsFeature> logger, 
            ServiceBackbone eventService,
            TicketsData ticketsData, 
            ViewerFeature viewerFeature) 
            : base(eventService)
        {
            _logger = logger;
            
            _autoPointsTimer = new Timer(300000); //5 minutes
            _autoPointsTimer.Elapsed += OnTimerElapsed;
            _viewerFeature = viewerFeature;
            _ticketsData = ticketsData;
            _autoPointsTimer.Start();
            
        }

        public async Task GiveTicketsToActiveAndSubsOnlineWithBonus(long amount, long bonusAmount) {
            var activeViewers =  _viewerFeature.GetActiveViewers();
            var onlineViewers = _viewerFeature.GetCurrentViewers();
            foreach(var viewer in onlineViewers) {
                if(await _viewerFeature.IsSubscriber(viewer)) {
                    activeViewers.Add(viewer);
                }
            }
            await GiveTicketsWithBonusToViewers(activeViewers.Distinct(), amount, bonusAmount);
        }

        public async Task GiveTicketsToActiveUsers(long amount) {
            var activeViewers =  _viewerFeature.GetActiveViewers();
            var onlineViewers = _viewerFeature.GetCurrentViewers();
            foreach(var viewer in onlineViewers) {
                if(await _viewerFeature.IsSubscriber(viewer)) {
                    activeViewers.Add(viewer);
                }
            }
            await GiveTicketsWithBonusToViewers(activeViewers.Distinct(), amount, 0);
        }

        public async Task GiveTicketsToAllOnlineViewersWithBonus(long amount, long bonusAmount) {
            var viewers = _viewerFeature.GetCurrentViewers();
            await GiveTicketsWithBonusToViewers(viewers, amount, bonusAmount);
        }
        
        public async Task GiveTicketsWithBonusToViewers(IEnumerable<string> viewers, long amount, long subBonusAmount)
        {
            foreach(var viewer in viewers) {
                long bonus = 0;
                var viewerData = await _viewerFeature.GetViewer(viewer);
                if(viewerData != null) {
                    bonus = viewerData.isSub ? subBonusAmount : 0; // Sub Bonus
                }
                await GiveTicketsToViewer(viewer, amount + bonus);
            }
        }

        public async Task<long> GiveTicketsToViewer(string viewer, long amount) {
            var viewerPoints = await _ticketsData.FindOne(viewer);
            if(viewerPoints == null) {
                viewerPoints = new Models.ViewerTickets(){
                    Username = viewer.ToLower(),
                    Points = 0
                };
            }
            viewerPoints.Points += amount;
            if(viewerPoints.Points < 0) {
                //Should NEVER hit this
                _logger.LogCritical("Points for {0} would have gone negative, points to remove {1}", viewer, amount);
                throw new Exception("Points would have went negative. ABORTING");
            }
            await _ticketsData.InsertOrUpdate(viewerPoints);
            _logger.LogInformation("Gave points to {0}", viewer);
            return viewerPoints.Points;
        }

        public async Task<long> GetViewerTickets(string viewer){
            var viewerPoints = await _ticketsData.FindOne(viewer);
            return viewerPoints == null ? 0 : viewerPoints.Points;
        }

         public async Task<bool> RemoveTicketsFromViewer(string viewer, long amount) {
            try{
                await GiveTicketsToViewer(viewer, -amount);
                return true;
            } catch (Exception) {
                return false;
            }
        }

        private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            var viewers = _viewerFeature.GetCurrentViewers();

            _logger.LogInformation("Currently a total of {0} viewers", viewers.Count());

            if(_eventService.IsOnline) {
                _logger.LogInformation("Starting to give  out tickets");
                await GiveTicketsToActiveAndSubsOnlineWithBonus(5, 5);
                await GiveTicketsToAllOnlineViewersWithBonus(1, 2);
            }
        }

        private async Task SayViewerTickets(CommandEventArgs e) {
            var viewer = await _ticketsData.FindOne(e.Name);
            await this._eventService.SendChatMessage(
                string.Format("@{0}, you have {1} testpoints.", 
                e.DisplayName,
                viewer != null ? viewer.Points : 0
                ));
        }
        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            switch(e.Command) {
                case "testpoints": {
                    await SayViewerTickets(e);
                    break;
                }
                case "givepoints":{
                    if(e.isMod && Int64.TryParse(e.Args[1], out long amount)) {
                        var totalPoints = await GiveTicketsToViewer(e.TargetUser, amount);
                        await _eventService.SendChatMessage(string.Format("Gave {0} {1} test points, {0} now has {2} test points.", e.TargetUser, amount, totalPoints));
                    }
                    break;
                }
                case "resetpoints" :{

                }
                break;
            }
        }
    }
}