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
        
        Timer _timer;
        private ViewerFeature _viewerFeature;
        private PointsData _pointsData;

        public PointsFeature(
            ILogger<PointsFeature> logger, 
            EventService eventService,
            PointsData pointsData, 
            ViewerFeature viewerFeature) 
            : base(eventService)
        {
            this._eventService.CommandEvent += OnCommand;
            _logger = logger;
            
            _timer = new Timer(300000); //5 minutes
            _timer.Elapsed += OnTimerElapsed;
            _viewerFeature = viewerFeature;
            _pointsData = pointsData;
        }

        public async Task GivePointsToActiveViewersWithBonus(int amount, int bonusAmount) {
            var activeViewers =  _viewerFeature.GetActiveViewers();
            await GivePointsWithBonusToViewers(activeViewers, amount, bonusAmount);
        }

        public async Task GivePointsToAllOnlineViewersWithBonus(int amount, int bonusAmount) {
            var viewers = _viewerFeature.GetCurrentViewers();
            await GivePointsWithBonusToViewers(viewers, amount, bonusAmount);
        }
        
        public async Task GivePointsWithBonusToViewers(List<string> viewers, int amount, int subBonusAmount)
        {
            foreach(var viewer in viewers) {
                var bonus = 0;
                var viewerData = await _viewerFeature.GetViewer(viewer);
                if(viewerData != null) {
                    bonus = viewerData.isSub ? subBonusAmount : 0; // Sub Bonus
                }
                await GivePointsToViewer(viewer, amount + bonus);
            }
        }

        public async Task<int> GivePointsToViewer(string viewer, int amount) {
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

        public async Task<int> GetViewerPoints(string viewer){
            var viewerPoints = await _pointsData.FindOne(viewer);
            return viewerPoints == null ? 0 : viewerPoints.Points;
        }

         public async Task<bool> RemovePointsFromViewer(string viewer, int amount) {
            try{
                await GivePointsToViewer(viewer, -amount);
                return true;
            } catch (Exception) {
                return false;
            }
        }

        private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            if(_eventService.IsOnline) {
                _logger.LogInformation("Starting to give  out tickets");
                await GivePointsToActiveViewersWithBonus(5, 5);
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
                    if(e.isMod && Int32.TryParse(e.Args[1], out int amount)) {
                        var totalPoints = await GivePointsToViewer(e.TargetUser, amount);
                        await _eventService.SendChatMessage(string.Format("Gave {0} {1} test points, {0} now has {2} test points.", e.TargetUser, amount, totalPoints));
                    }
                    break;
                }
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

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _timer.Start();
            return Task.CompletedTask;
        }
    }
}