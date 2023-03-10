using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;

namespace DotNetTwitchBot.Bot.Commands.Games
{
    public abstract class BaseRaffle : BaseCommand
    {
        DateTime _startTime = DateTime.Now;
        Timer _intervalTimer;

        int _runTime = 75;
        enum State {
            NotRunning,
            Running,
            Closing
        }
        State CurrentState {get;set;} = State.NotRunning;

        List<string> _entered = new List<string>();
        bool _joinedSinceLastAnnounce = false;

        //Variables to be set by descendants
        protected string _emote;
        protected string _command;
        protected string _name;
        private TicketsFeature _ticketsFeature;

        protected int WinAmount {get;set;} = 0;

        protected int NumberOfWinners {get;set;} = 3;
        protected int NumberEntered {get{return _entered.Count;}}

        protected BaseRaffle(
            ServiceBackbone eventService,
            TicketsFeature ticketsFeature,
            string emote,
            string command,
            string name
        ) : base(eventService)
        {
            _emote = emote;
            _command = command;
            _name = name;
            _ticketsFeature = ticketsFeature;
            _intervalTimer = new Timer(timerCallBack, this, Timeout.Infinite, Timeout.Infinite);
        }

        public async Task StartRaffle(string Sender, int amountToWin) {
            if(CurrentState != State.NotRunning) {
                await _eventService.SendChatMessage(Sender, string.Format(raffleRunning, "name"));
            }

            CurrentState = State.Running;
            WinAmount = new Random().Next(Convert.ToInt32(amountToWin * 0.66), amountToWin);
            _startTime = DateTime.Now;
            _entered.Clear();
            _joinedSinceLastAnnounce = false;
            await _eventService.SendChatMessage(string.Format(raffleStarting, _emote, WinAmount, _command));
            RunRaffle();
        }

        public async Task UpdateOrStopRaffle() {
            var elapsedTime = DateTime.Now - _startTime;
            if(elapsedTime.TotalSeconds > _runTime) {
                _intervalTimer.Change(Timeout.Infinite, Timeout.Infinite);
                UpdateNumberOfWinners();
                await PickWinners();
                return; //Raffle Ended
            }
            await SendJoinedMessage();
            await SendTimeLeft((int)elapsedTime.TotalSeconds);
        }

        protected virtual void UpdateNumberOfWinners() {

        }

        private void RunRaffle() {
            _intervalTimer.Change(15000,15000);
        }

        private static async void timerCallBack(object? state)
        {
            if(state == null) return;
            var raffle = (BaseRaffle)state;
            await raffle.UpdateOrStopRaffle();
        }

        

        private async Task PickWinners() {
            CurrentState = State.Closing;
            if(_entered.Count == 0) {
                CurrentState = State.NotRunning;
                await _eventService.SendChatMessage(string.Format(raffleNotEnough, _name));
                return;
            }
            
            var winnerCount = 0;
            if(_entered.Count < NumberOfWinners) {
                winnerCount = _entered.Count;
            } else {
                winnerCount = NumberOfWinners;
            }

            var winners = new List<string>();
            for(var n = 0; n < winnerCount; n++) {
                var winner = _entered.RandomElement();
                _entered.RemoveAll(x => x.ToLower().Equals(winner.ToLower()));
                winners.Add(winner);
            }

            var eachWins = Math.Ceiling((double)WinAmount / winnerCount);
            await _eventService.SendChatMessage(string.Format(raffleWinners, string.Join(", ", winners), _name, eachWins, _emote));
            foreach(var winner in winners) {
                await _ticketsFeature.GiveTicketsToViewer(winner, (long)eachWins);
            }
            CurrentState = State.NotRunning;
        }   

        private async Task SendTimeLeft(int elapsedTime) {
            await _eventService.SendChatMessage(string.Format(raffleTimeLeft,_runTime - elapsedTime, _command, WinAmount));
        }

        private async Task SendJoinedMessage() {
            if(_joinedSinceLastAnnounce) {
                await _eventService.SendChatMessage(string.Format(raffleJoined, _name));
                _joinedSinceLastAnnounce = false;
            }
        }

        protected async Task EnterRaffle(string username) {
            if(CurrentState != State.Running) {
                return;
            }

            if(_entered.Exists(x => x.ToLower().Equals(username.ToLower()))) {
                await _eventService.SendChatMessage(username, alreadyJoined);
                return;
            }
            _entered.Add(username);
            _joinedSinceLastAnnounce = true;
        }

        //Strings
        private string raffleRunning = "{0} is already running";
        private string raffleStarting = "{0} a ticket raffle for {1} giveaway tickets has started! type {2} to join! {0}";
        private string raffleJoined = "Penguins have joined the {0}";
        private string raffleTimeLeft = "{0} seconds left to type {1} for a chance of extra tickets {2}";
        private string raffleNotEnough = "Nobody joined the {0} so no body wins.";
        private string raffleWinners = "{0} are the winners of the {1}! They each get {2} tickets. {3} {3} {3}";
        private string alreadyJoined = "You already joined the {0}";
    }
}