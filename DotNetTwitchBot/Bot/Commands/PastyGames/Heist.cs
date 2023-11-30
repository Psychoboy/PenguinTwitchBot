using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class Heist : BaseCommandService
    {
        private readonly ILoyaltyFeature _loyaltyFeature;
        private readonly int Cooldown = 300;
        private readonly int JoinTime = 300;
        private readonly int MinBet = 10;
        private readonly List<Participant> Entered = new();
        private readonly List<Participant> Survivors = new();
        private readonly List<Participant> Caught = new();
        private readonly Timer JoinTimer;
        private readonly ILogger<Heist> _logger;
        private State GameState = State.NotRunning;
        private int CurrentStoryPart = 0;
        private readonly string CommandName = "heist";

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
            ILoyaltyFeature loyaltyFeature,
            IServiceBackbone serviceBackbone,
            ILogger<Heist> logger,
            ICommandHandler commandHandler
            ) : base(serviceBackbone, commandHandler)
        {
            _loyaltyFeature = loyaltyFeature;
            JoinTimer = new Timer(JoinTimerCallback, this, Timeout.Infinite, Timeout.Infinite);
            _logger = logger;
        }

        public override async Task Register()
        {
            var moduleName = "Heist";
            await RegisterDefaultCommand("heist", this, moduleName);
            _logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            if (!command.CommandProperties.CommandName.Equals(CommandName)) return;

            if (GameState == State.Finishing)
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, "you can not join the heist now.");
                throw new SkipCooldownException();
            }

            if (Entered.Exists(x => x.Name.Equals(e.Name)))
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, "you have already joined the heist.");
                throw new SkipCooldownException();
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
                await ServiceBackbone.SendChatMessage(e.DisplayName,
                "To join the heist, enter !heist AMOUNT or ALL or MAX");
                throw new SkipCooldownException();
            }

            if (amount > LoyaltyFeature.MaxBet || amount < MinBet)
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, string.Format("The max amount to join the heist is {0} and must be greater then {1}", LoyaltyFeature.MaxBet.ToString("N0"), MinBet));
                throw new SkipCooldownException();
            }

            if (!(await _loyaltyFeature.RemovePointsFromUser(e.Name, amount)))
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, "sorry you don't have that amount to enter the heist.");
                throw new SkipCooldownException();
            }

            if (GameState == State.NotRunning)
            {
                await ServiceBackbone.SendChatMessage(string.Format("{0} is trying to get a team together for some serious heist business! use \"!heist AMOUNT/ALL/MAX\" to join!", e.DisplayName));
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
                        await ServiceBackbone.SendChatMessage("The Fin Fam sptvTFF gets ready to steal some pasties from Charlie! SHARK");
                        JoinTimer.Change(5000, 5000);
                        CurrentStoryPart++;
                        return;

                    case 1:
                        await ServiceBackbone.SendChatMessage("Everyone sharpens their beaks, brushes their feathers, and gets ready to sneak past Charlie!");
                        CurrentStoryPart++;
                        return;

                    case 2:
                        if (Caught.Count > 0)
                        {
                            await ServiceBackbone.SendChatMessage(string.Format("Look out! Charlie SHARK captured {0}", GetCaughtNames()));
                        }
                        CurrentStoryPart++;
                        return;

                    case 3:
                        if (Survivors.Count > 0)
                        {
                            await ServiceBackbone.SendChatMessage(string.Format("{0} sptvTFF managed to sneak past Charlie sharkS and grab some of those precious pasties!", GetWinnerNames()));
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
                    await ServiceBackbone.SendChatMessage("The heist ended! There are no survivors.");
                }
                else if (((maxlength + 14) + "superpenguintv".Length) > 512)
                {
                    await ServiceBackbone.SendChatMessage(string.Format("The heist ended with {0} survivor(s) and {1} death(s).", Survivors.Count, Caught.Count));
                }
                else
                {
                    await ServiceBackbone.SendChatMessage(string.Format("The heist ended! Survivors are: {0}.", string.Join(",", payouts)));
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
                CommandHandler.AddGlobalCooldown(CommandName, Cooldown);
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
                var result = Tools.RandomRange(1, 100);
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