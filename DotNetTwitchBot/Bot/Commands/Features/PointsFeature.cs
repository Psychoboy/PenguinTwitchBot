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
    public class PointsFeature : BaseFeature
    {
        private readonly ILogger<PointsFeature> _logger;
        
        Timer _autoPointsTimer;
        Timer _ticketsToActiveCommandTimer;
        private ViewerFeature _viewerFeature;
        private PointsData _pointsData;

        private long _ticketsToGiveOut = 0;
        private DateTime _lastTicketsAdded = DateTime.Now;

        public PointsFeature(
            ILogger<PointsFeature> logger, 
            EventService eventService,
            PointsData pointsData, 
            ViewerFeature viewerFeature) 
            : base(eventService)
        {
            this._eventService.CommandEvent += OnCommand;
            _logger = logger;
            
            _autoPointsTimer = new Timer(300000); //5 minutes
            _autoPointsTimer.Elapsed += OnTimerElapsed;

            _ticketsToActiveCommandTimer = new Timer(1000);
            _ticketsToActiveCommandTimer.Elapsed += OnActiveCommandTimerElapsed;

            _viewerFeature = viewerFeature;
            _pointsData = pointsData;
            _autoPointsTimer.Start();
            _ticketsToActiveCommandTimer.Start();
        }

        public async Task GivePointsToActiveAndSubsOnlineWithBonus(long amount, long bonusAmount) {
            var activeViewers =  _viewerFeature.GetActiveViewers();
            var onlineViewers = _viewerFeature.GetCurrentViewers();
            foreach(var viewer in onlineViewers) {
                if(await _viewerFeature.IsSubscriber(viewer)) {
                    activeViewers.Add(viewer);
                }
            }
            await GivePointsWithBonusToViewers(activeViewers.Distinct(), amount, bonusAmount);
        }

        public async Task GivePointsToActiveUsers(long amount) {
            var activeViewers =  _viewerFeature.GetActiveViewers();
            var onlineViewers = _viewerFeature.GetCurrentViewers();
            foreach(var viewer in onlineViewers) {
                if(await _viewerFeature.IsSubscriber(viewer)) {
                    activeViewers.Add(viewer);
                }
            }
            await GivePointsWithBonusToViewers(activeViewers.Distinct(), amount, 0);
        }

        public async Task GivePointsToAllOnlineViewersWithBonus(long amount, long bonusAmount) {
            var viewers = _viewerFeature.GetCurrentViewers();
            await GivePointsWithBonusToViewers(viewers, amount, bonusAmount);
        }
        
        public async Task GivePointsWithBonusToViewers(IEnumerable<string> viewers, long amount, long subBonusAmount)
        {
            foreach(var viewer in viewers) {
                long bonus = 0;
                var viewerData = await _viewerFeature.GetViewer(viewer);
                if(viewerData != null) {
                    bonus = viewerData.isSub ? subBonusAmount : 0; // Sub Bonus
                }
                await GivePointsToViewer(viewer, amount + bonus);
            }
        }

        public async Task<long> GivePointsToViewer(string viewer, long amount) {
            var viewerPoints = await _pointsData.FindOne(viewer);
            if(viewerPoints == null) {
                viewerPoints = new Models.ViewerPoints(){
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
            await _pointsData.InsertOrUpdate(viewerPoints);
            _logger.LogInformation("Gave points to {0}", viewer);
            return viewerPoints.Points;
        }

        public async Task<long> GetViewerPoints(string viewer){
            var viewerPoints = await _pointsData.FindOne(viewer);
            return viewerPoints == null ? 0 : viewerPoints.Points;
        }

         public async Task<bool> RemovePointsFromViewer(string viewer, long amount) {
            try{
                await GivePointsToViewer(viewer, -amount);
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
                await GivePointsToActiveAndSubsOnlineWithBonus(5, 5);
                await GivePointsToAllOnlineViewersWithBonus(1, 2);
            }
        }
        
       
        private async Task OnCommand(object? sender, CommandEventArgs e)
        {
            switch(e.Command) {
                case "testpoints": {
                    await SayViewerPoints(e.Sender);
                    break;
                }
                case "givepoints":{
                    if(e.isMod && Int64.TryParse(e.Args[1], out long amount)) {
                        var totalPoints = await GivePointsToViewer(e.TargetUser, amount);
                        await _eventService.SendChatMessage(string.Format("Gave {0} {1} test points, {0} now has {2} test points.", e.TargetUser, amount, totalPoints));
                    }
                    break;
                }
                case "addactivetest": {
                    if((e.isMod) && Int64.TryParse(e.Args[0], out long amount)) {
                        _lastTicketsAdded = DateTime.Now;
                        _ticketsToGiveOut += amount;
                    }
                    break;
                }
            }
        }

        private async void OnActiveCommandTimerElapsed(object? sender, ElapsedEventArgs e){
            if(_ticketsToGiveOut > 0 && _lastTicketsAdded.AddSeconds(5) < DateTime.Now) {
                    await GivePointsToActiveUsers(_ticketsToGiveOut);
                    await _eventService.SendChatMessage(string.Format("Sending {0} tickets to all active users.", _ticketsToGiveOut));
                    _ticketsToGiveOut = 0;
            }
        }

        private async Task SayViewerPoints(string sender) {
            var viewer = await _pointsData.FindOne(sender);
            await this._eventService.SendChatMessage(
                string.Format("@{0}, you have {1} testpoints.", 
                sender,
                viewer != null ? viewer.Points : 0
                ));
        }
    }
}