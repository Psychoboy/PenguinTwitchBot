using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Extensions;

namespace DotNetTwitchBot.Bot.Commands.TicketGames
{
    public abstract class BaseRaffle : BaseCommandService, IHostedService
    {
        DateTime _startTime = DateTime.Now;
        readonly Timer _intervalTimer;
        protected readonly ILogger _logger;
        readonly int _runTime = 90;
        enum State
        {
            NotRunning,
            Running,
            Closing
        }
        State CurrentState { get; set; } = State.NotRunning;

        readonly List<string> _entered = [];
        bool _joinedSinceLastAnnounce = false;

        //Variables to be set by descendants
        protected string _emote;
        protected string _command;
        protected string _name;
        protected readonly IPointsSystem _pointSystem;

        protected int WinAmount { get; set; } = 0;

        protected int NumberOfWinners { get; set; } = 3;
        protected int NumberEntered { get { return _entered.Count; } }
        private bool RemainingTimeSent { get; set; } = false;

        protected BaseRaffle(
            IServiceBackbone eventService,
            IPointsSystem pointSystem,
            ICommandHandler commandHandler,
            string emote,
            string command,
            string name,
            ILogger logger
        ) : base(eventService, commandHandler, name)
        {
            _emote = emote;
            _command = command;
            _name = name;
            _pointSystem = pointSystem;
            _intervalTimer = new Timer(TimerCallBack, this, Timeout.Infinite, Timeout.Infinite);
            _logger = logger;
        }

        public async Task StartRaffle(string Sender, int amountToWin)
        {
            if (CurrentState != State.NotRunning)
            {
                await ServiceBackbone.SendChatMessage(Sender, string.Format(raffleRunning, _name));
                return;
            }

            CurrentState = State.Running;
            WinAmount = StaticTools.Next(Convert.ToInt32(amountToWin * 0.66), amountToWin + 1);
            _startTime = DateTime.Now;
            _entered.Clear();
            _joinedSinceLastAnnounce = false;
            await ServiceBackbone.SendChatMessage(string.Format(raffleStarting, _emote, WinAmount, _command));
            RunRaffle();
        }

        public async Task UpdateOrStopRaffle()
        {
            var elapsedTime = DateTime.Now - _startTime;
            if (elapsedTime.TotalSeconds > _runTime)
            {
                _intervalTimer.Change(Timeout.Infinite, Timeout.Infinite);
                UpdateNumberOfWinners();
                await PickWinners();
                RemainingTimeSent = false;
                return; //Raffle Ended
            }
            //await SendJoinedMessage();
            if (RemainingTimeSent == false && (int)elapsedTime.TotalSeconds >= _runTime / 2)
                await SendTimeLeft((int)elapsedTime.TotalSeconds);
        }

        protected virtual void UpdateNumberOfWinners()
        {
            //Does nothing and is only called my child classes.
        }

        private void RunRaffle()
        {
            _intervalTimer.Change(15000, 15000);
        }

        private static async void TimerCallBack(object? state)
        {
            if (state == null) return;
            var raffle = (BaseRaffle)state;
            await raffle.UpdateOrStopRaffle();
        }



        private async Task PickWinners()
        {
            CurrentState = State.Closing;
            if (_entered.Count == 0)
            {
                CurrentState = State.NotRunning;
                await ServiceBackbone.SendChatMessage(string.Format(raffleNotEnough, _name));
                return;
            }

            var winnerCount = 0;
            if (_entered.Count < NumberOfWinners)
            {
                winnerCount = _entered.Count;
            }
            else
            {
                winnerCount = NumberOfWinners;
            }

            var winners = new List<string>();
            for (var n = 0; n < winnerCount; n++)
            {
                var winner = _entered.RandomElementOrDefault();
                _entered.RemoveAll(x => x.ToLower().Equals(winner.ToLower()));
                winners.Add(winner);
            }

            var eachWins = Math.Ceiling((double)WinAmount / winnerCount);
            await ServiceBackbone.SendChatMessage(string.Format(raffleWinners, string.Join(", ", winners), _name, eachWins, _emote));
            foreach (var winner in winners)
            {
                await _pointSystem.AddPointsByUsernameAndGame(winner, "raffle", (long)eachWins);
            }
            CurrentState = State.NotRunning;
        }

        private async Task SendTimeLeft(int elapsedTime)
        {
            if (_runTime - elapsedTime < 10) return;
            RemainingTimeSent = true;
            await ServiceBackbone.SendChatMessage(string.Format(raffleTimeLeft, _runTime - elapsedTime, _command, WinAmount));
        }

        private async Task SendJoinedMessage()
        {
            if (_joinedSinceLastAnnounce)
            {
                await ServiceBackbone.SendChatMessage(string.Format(raffleJoined, _name));
                _joinedSinceLastAnnounce = false;
            }
        }

        protected void EnterRaffle(CommandEventArgs e)
        {
            if (CurrentState != State.Running)
            {
                return;
            }
            var username = e.Name;
            if (_entered.Exists(x => x.ToLower().Equals(username.ToLower())))
            {
                return;
            }
            _entered.Add(username);
            _joinedSinceLastAnnounce = true;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting {moduledname}", ModuleName);
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopped {moduledname}", ModuleName);
            return Task.CompletedTask;
        }

        //Strings
        private readonly string raffleRunning = "{0} is already running";
        private readonly string raffleStarting = "{0} a ticket raffle for {1} giveaway tickets has started! type {2} to join! {0}";
        private readonly string raffleJoined = "Penguins have joined the {0}";
        private readonly string raffleTimeLeft = "{0} seconds left to type {1} for a chance of extra tickets {2}";
        private readonly string raffleNotEnough = "Nobody joined the {0} so no body wins.";
        private readonly string raffleWinners = "{0} are the winners of the {1}! They each get {2} tickets. {3} {3} {3}";
        //private readonly string alreadyJoined = "You already joined the {0}";
    }
}
