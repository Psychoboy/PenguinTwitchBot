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
    public class TicketFeature : BaseFeature
    {
        private readonly ILogger<TicketFeature> _logger;
        
        Timer _timer;

        // //Temp Table for Tracking Points
        // private Dictionary<string, int> _tickets = new Dictionary<string, int>();
        private UserFeature _userFeature;
        private IDbViewerPoints _dbViewerPoints;

        public TicketFeature(
            ILogger<TicketFeature> logger, 
            EventService eventService,
            IDbViewerPoints dbViewerPoints, 
            UserFeature userFeature) 
            : base(eventService)
        {
            this._eventService.CommandEvent += OnCommand;
            _logger = logger;
            
            _timer = new Timer(10000);
            _timer.Elapsed += OnTimerElapsed;
            _userFeature = userFeature;
            _dbViewerPoints = dbViewerPoints;
        }

        public void GiveTicketsToActiveUsers(int amount) {
             var activeUsers = _userFeature.GetActiveUsers();
            foreach(var activeUser in activeUsers) {
                GiveTicketsToUser(activeUser, amount);
            }
        }

        public int GiveTicketsToUser(string user, int amount) {
            var viewer = _dbViewerPoints.FindOne(user);
            if(viewer == null) {
                viewer = new Models.ViewerPoints(){
                    Username = user.ToLower(),
                    Points = 0
                };
            }
            viewer.Points += amount;
            _dbViewerPoints.InsertOrUpdate(viewer);
            _logger.LogInformation("Gave points to {0}", user);
            return viewer.Points;
        }

        private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            _logger.LogInformation("Starting to give  out tickets");
            GiveTicketsToActiveUsers(5);
        }
        
        public void RemoveTicketsFromUser(string user, int amount) {
            GiveTicketsToUser(user, -amount);
        }
        private async Task OnCommand(object? sender, CommandEventArgs e)
        {
            switch(e.Command) {
                case "testpoints": {
                    await SendUserPoints(e.Sender);
                    break;
                }
                case "givepoints":{
                    if(e.isMod && Int32.TryParse(e.Args[1], out int amount)) {
                        var totalPoints = GiveTicketsToUser(e.TargetUser, amount);
                        await _eventService.SendChatMessage(string.Format("Gave {0} {1} test points, {0} now has {2} test points.", e.TargetUser, amount, totalPoints));
                    }
                    break;
                }
            }
            
        }

        private async Task SendUserPoints(string sender) {
            var viewer = _dbViewerPoints.FindOne(sender);
            var test = _dbViewerPoints.FindAll().ToList();
            await this._eventService.SendChatMessage(
                string.Format("@{0}, you have {1} testpoints.", 
                sender,
                viewer != null ? viewer.Points : 0
                ));
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _timer.Start();
            return Task.CompletedTask;
        }
    }
}