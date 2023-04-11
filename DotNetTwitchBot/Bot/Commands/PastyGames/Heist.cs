using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class Heist : BaseCommand
    {
        private LoyaltyFeature _loyaltyFeature;
        private ViewerFeature _viewerFeature;
        private readonly int Cooldown = 300;
        private readonly int JoinTime = 300;
        private readonly int MinBet = 10;
        private List<Participant> Entered = new List<Participant>();
        private List<Participant> Survivors = new List<Participant>();
        private List<Participant> Caught = new List<Participant>();
        private Timer JoinTimer;
        private ILogger<Heist> _logger;
        private State GameState = State.NotRunning;
        private int CurrentStoryPart = 0;
        private string Command = "heist";

        enum State
        {
            NotRunning,
            Running,
            Finishing
        }

        private class Participant
        {
            public string Name = null!;
            public string DisplayName = null!;
            public Int64 Bet = 0;
        }

        public Heist(
            LoyaltyFeature loyaltyFeature,
            ViewerFeature viewerFeature,
            ServiceBackbone serviceBackbone,
            ILogger<Heist> logger
            ) : base(serviceBackbone)
        {
            _loyaltyFeature = loyaltyFeature;
            _viewerFeature = viewerFeature;
            JoinTimer = new Timer(JoinTimerCallback, this, Timeout.Infinite, Timeout.Infinite);
            _logger = logger;
        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            if (!e.Command.Equals(Command)) return;
            if (!IsCoolDownExpired(e.Name, e.Command))
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, "!heist is still on cooldown");
                return;
            }
            if (GameState == State.Finishing)
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, "you can not join the heist now.");
                return;
            }

            if (Entered.Exists(x => x.Name.Equals(e.Name)))
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, "you have already joined the heist.");
                return;
            }

            var amountStr = e.Args.First();
            var amount = 0L;
            if (amountStr.Equals("all", StringComparison.CurrentCultureIgnoreCase) ||
               amountStr.Equals("max", StringComparison.CurrentCultureIgnoreCase))
            {
                amount = await _loyaltyFeature.GetMaxPointsFromUser(e.Name);
            }
            else if (!Int64.TryParse(amountStr, out amount))
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName,
                "To join the heist, enter !heist AMOUNT or ALL or MAX");
                return;
            }

            if (amount > LoyaltyFeature.MaxBet || amount < MinBet)
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, string.Format("The max amount to join the heist is {0} and must be greater then {1}", LoyaltyFeature.MaxBet.ToString("N0"), MinBet));
                return;
            }

            if (!(await _loyaltyFeature.RemovePointsFromUser(e.Name, amount)))
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, "sorry you don't have that amount to enter the heist.");
                return;
            }

            if (GameState == State.NotRunning)
            {
                await _serviceBackbone.SendChatMessage(string.Format("{0} is trying to get a team together for some serious heist business! use \"!heist AMOUNT/ALL/MAX\" to join!", e.DisplayName));
                GameState = State.Running;
                JoinTimer.Change(JoinTime * 1000, JoinTime * 1000);
            }
            Entered.Add(new Participant
            {
                Name = e.Name,
                DisplayName = e.DisplayName,
                Bet = amount
            });
        }

        private async void JoinTimerCallback(object? state)
        {
            if (state == null)
            {
                _logger.LogError("State was null, state should never be null!");
                return;
            }

            var heist = (Heist)state;
            await heist.RunStory();
        }

        private async Task RunStory()
        {
            GameState = State.Finishing;
            try
            {
                switch (CurrentStoryPart)
                {
                    case 0:
                        CalculateResult();
                        await _serviceBackbone.SendChatMessage("The Fin Fam sptvTFF gets ready to steal some pasties from Charlie! SHARK");
                        JoinTimer.Change(5000, 5000);
                        CurrentStoryPart++;
                        return;

                    case 1:
                        await _serviceBackbone.SendChatMessage("Everyone sharpens their beaks, brushes their feathers, and gets ready to sneak past Charlie!");
                        CurrentStoryPart++;
                        return;

                    case 2:
                        if (Caught.Count > 0)
                        {
                            await _serviceBackbone.SendChatMessage(string.Format("Look out! Charlie SHARK captured {0}", GetCaughtNames()));
                        }
                        CurrentStoryPart++;
                        return;

                    case 3:
                        if (Survivors.Count > 0)
                        {
                            await _serviceBackbone.SendChatMessage(string.Format("{0} sptvTFF managed to sneak past Charlie sharkS and grab some of those precious pasties!", GetWinnerNames()));
                        }
                        CurrentStoryPart++;
                        return;
                }

            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed finishing heist");
                await EndHeist();
            }
            await EndHeist();
        }


        private async Task EndHeist()
        {
            try
            {
                JoinTimer.Change(Timeout.Infinite, Timeout.Infinite);
                var maxlength = 0;
                var payouts = new List<string>();
                foreach (var participant in Survivors)
                {
                    var pay = Convert.ToInt64(participant.Bet * 1.5);
                    await _loyaltyFeature.AddPointsToViewer(participant.Name, participant.Bet + pay);
                    var formattedName = string.Format("{0} ({1})", participant.DisplayName, (participant.Bet + pay).ToString("N0"));
                    maxlength += formattedName.Length;
                    payouts.Add(formattedName);
                }

                if (payouts.Count == 0)
                {
                    await _serviceBackbone.SendChatMessage("The heist ended! There are no survivors.");
                }
                else if (((maxlength + 14) + "superpenguintv".Length) > 512)
                {
                    await _serviceBackbone.SendChatMessage(string.Format("The heist ended with {0} survivor(s) and {1} death(s).", Survivors.Count, Caught.Count));
                }
                else
                {
                    await _serviceBackbone.SendChatMessage(string.Format("The heist ended! Survivors are: {0}.", string.Join(",", payouts)));
                }
                CleanUp();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed Ending heist");
                CleanUp();
            }
        }

        private void CleanUp()
        {
            try
            {
                Entered.Clear();
                Survivors.Clear();
                Caught.Clear();
                GameState = State.NotRunning;
                CurrentStoryPart = 0;
                AddGlobalCooldown(Command, Cooldown);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed Cleaning heist");
                GameState = State.NotRunning;
                CurrentStoryPart = 0;
            }
        }

        private string GetWinnerNames()
        {
            return string.Join(", ", Survivors.Select(x => x.DisplayName));
        }

        private string GetCaughtNames()
        {
            return string.Join(", ", Caught.Select(x => x.DisplayName));
        }

        private void CalculateResult()
        {
            foreach (var participant in Entered)
            {
                var result = Tools.RandomRange(1, 100); //Tools.CurrentThreadRandom.Next(0, 100);
                if (result > 40)
                {
                    Survivors.Add(participant);
                }
                else
                {
                    Caught.Add(participant);
                }
            }
        }
    }
}