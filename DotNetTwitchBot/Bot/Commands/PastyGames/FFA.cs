using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Alerts;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class FFA : BaseCommand
    {
        public int Cooldown = 300;
        public int JoinTime = 180;
        public int Cost = 100;
        public List<string> Entered = new List<string>();
        private Timer _joinTimer;
        private LoyaltyFeature _loyaltyFeature;
        private ViewerFeature _viewFeature;
        string Command = "testffa";

        enum State
        {
            NotRunning,
            Running,
            Finishing
        }

        private State GameState { get; set; }

        public FFA(
            LoyaltyFeature loyaltyFeature,
            ServiceBackbone serviceBackbone,
            ViewerFeature viewerFeature
            ) : base(serviceBackbone)
        {
            _joinTimer = new Timer(joinTimerCallback, this, Timeout.Infinite, Timeout.Infinite);
            _loyaltyFeature = loyaltyFeature;
            _viewFeature = viewerFeature;
        }

        private static void joinTimerCallback(object? state)
        {
            if (state == null) return;
            var ffa = (FFA)state;
            ffa.Finish();
        }

        private async void Finish()
        {
            GameState = State.Finishing;
            if (Entered.Count == 1)
            {
                await _loyaltyFeature.AddPointsToViewer(Entered[0], Cost);
                await _serviceBackbone.SendChatMessage("Not enough viewers joined the FFA, returning the fees.");
                CleanUp();
                return;
            }

            var winnerIndex = Tools.CurrentThreadRandom.Next(0, Entered.Count - 1);
            var winner = Entered[winnerIndex];
            var winnings = Entered.Count * Cost;
            await _serviceBackbone.SendChatMessage(string.Format("The dust finally settled and the last one standing is {0}", await _viewFeature.GetDisplayName(winner)));
            await _loyaltyFeature.AddPointsToViewer(winner, winnings);
            CleanUp();
        }

        private void CleanUp()
        {
            Entered.Clear();
            GameState = State.NotRunning;
            _joinTimer.Change(Timeout.Infinite, Timeout.Infinite);
            AddGlobalCooldown(Command, Cooldown);
        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {

            if (!e.Command.Equals(Command)) return;
            if (!IsCoolDownExpired(e.Name, e.Command)) return;

            if (GameState == State.Finishing)
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, string.Format("Sorry you were to late to join this one"));
                return;
            }

            if (Entered.Contains(e.Name))
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, string.Format("You have already joined the FFA!"));
                return;
            }

            if (!(await _loyaltyFeature.RemovePointsFromUser(e.Name, Cost)))
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, string.Format("Sorry it costs {0} to enter the FFA, which you do not have.", Cost));
                return;
            }

            if (GameState == State.NotRunning)
            {
                await _serviceBackbone.SendChatMessage(string.Format("{0} is starting a FFA battle! Type !ffa to join now!", e.DisplayName));
                GameState = State.Running;
                _joinTimer.Change(JoinTime * 1000, Timeout.Infinite);
            }
            else
            {
                await _serviceBackbone.SendChatMessage(string.Format("{0} joined the FFA", e.DisplayName));
            }
            Entered.Add(e.Name);
        }
    }
}