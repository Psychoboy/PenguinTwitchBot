using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Commands.Games;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class Heist(
        IPointsSystem pointsSystem,
        IGameSettingsService gameSettingsService,
        IServiceBackbone serviceBackbone,
        ILogger<Heist> logger,
        ICommandHandler commandHandler,
        TimeProvider timeProvider,
        ITools tools
            ) : BaseCommandService(serviceBackbone, commandHandler, GAMENAME), IHostedService
    {
        private readonly List<Participant> Entered = [];
        private readonly List<Participant> Survivors = [];
        private readonly List<Participant> Caught = [];
        private ITimer? JoinTimer;
        private State GameState = State.NotRunning;
        private int CurrentStoryPart = 0;
        private readonly string CommandName = "heist";

        //Members for Game Settings
        public static readonly string GAMENAME = "Heist";

        //Int settings for the game
        public static readonly string COOLDOWN = "Cooldown";
        public static readonly string JOINTIME = "JoinTime";
        public static readonly string MINBET = "MinBet";
        public static readonly string WIN_MULTIPLIER = "WinMultiplier";

        //String settings for the game
        public static readonly string GAMEFINISHING = "GameFinishing";
        public static readonly string GAMESTARTING = "GameStarting";
        public static readonly string ALREADYJOINED = "AlreadyJoined";
        public static readonly string INVALIDARGS = "NoArgs";
        public static readonly string INVALIDBET = "InvalidBet";
        public static readonly string NOTENOUGHPOINTS = "NotEnoughPoints";
        public static readonly string STAGEONE = "StageOne";
        public static readonly string STAGETWO = "StageTwo";
        public static readonly string STAGETHREE = "StageThree";
        public static readonly string STAGEFOUR = "StageFour";
        public static readonly string NOONEWON = "NoOneWon";
        public static readonly string NAMESTOLONG = "HeistEndedWithBoth";
        public static readonly string SURVIVORS = "HeistEnded";

        public enum State
        {
            NotRunning,
            Running,
            Finishing
        }

        public class Participant
        {
            public string Name = null!;
            public string DisplayName = null!;
            public long Bet = 0;
        }

        public override async Task Register()
        {
            var moduleName = "Heist";
            await RegisterDefaultCommand("heist", this, moduleName);
            await pointsSystem.RegisterDefaultPointForGame(ModuleName);
            logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            if (!command.CommandProperties.CommandName.Equals(CommandName)) return;

            if (GameState == State.Finishing)
            {
                var message = await gameSettingsService.GetStringSetting(GAMENAME, GAMEFINISHING, "you can not join the heist now.");
                await ServiceBackbone.SendChatMessage(e.DisplayName, message); //GAMEFINISHING
                throw new SkipCooldownException();
            }

            if (Entered.Exists(x => x.Name.Equals(e.Name)))
            {
                var message = await gameSettingsService.GetStringSetting(GAMENAME, ALREADYJOINED, "you have already joined the heist.");
                await ServiceBackbone.SendChatMessage(e.DisplayName, message); //ALREADYJOINED
                throw new SkipCooldownException();
            }

            if (e.Args.Count == 0)
            {
                await SendInvalidArgsMessage(e.DisplayName, e.Command);
                throw new SkipCooldownException();
            }

            var amountStr = e.Args.First();
            var amount = 0L;
            if (amountStr.Equals("all", StringComparison.OrdinalIgnoreCase) ||
               amountStr.Equals("max", StringComparison.OrdinalIgnoreCase))
            {
                amount = await pointsSystem.GetMaxPointsByUserIdAndGame(e.UserId, ModuleName, PointsSystem.MaxBet);
            }
            else if (amountStr.Contains('%'))
            {
                try
                {
                    var result = new Percentage(amountStr);
                    if (result.Value <= 0 || result.Value > 100)
                    {
                        await SendInvalidArgsMessage(e.DisplayName, e.Command);
                        throw new SkipCooldownException();
                    }
                    amount = (long)(await pointsSystem.GetMaxPointsByUserIdAndGame(e.UserId, ModuleName, PointsSystem.MaxBet) * result.Value);
                }
                catch
                {
                    await SendInvalidArgsMessage(e.DisplayName, e.Command);
                    throw new SkipCooldownException();
                }
            }
            else if (!Int64.TryParse(amountStr, out amount))
            {
                await SendInvalidArgsMessage(e.DisplayName, e.Command);
                throw new SkipCooldownException();
            }
            var pointType = await pointsSystem.GetPointTypeForGame(GAMENAME);
            var minBet = await gameSettingsService.GetIntSetting(GAMENAME, MINBET, 10);
            if (amount > LoyaltyFeature.MaxBet || amount < minBet)
            {
                var message = await gameSettingsService.GetStringSetting(GAMENAME, INVALIDBET, "The max amount to join the heist is {MaxBet} {PointType} and must be greater then {MinBet} {PointType}");
                message = message
                    .Replace("{MaxBet}", LoyaltyFeature.MaxBet.ToString("N0"), StringComparison.OrdinalIgnoreCase)
                    .Replace("{MinBet}", minBet.ToString("N0"), StringComparison.OrdinalIgnoreCase)
                    .Replace("{PointType}", pointType.Name, StringComparison.OrdinalIgnoreCase);
                await ServiceBackbone.SendChatMessage(e.DisplayName, message); //INVALIDBET
                throw new SkipCooldownException();
            }

            if (!(await pointsSystem.RemovePointsFromUserByUserIdAndGame(e.UserId, ModuleName, amount)))
            {
                var message = await gameSettingsService.GetStringSetting(GAMENAME, NOTENOUGHPOINTS, "sorry you don't have that amount of {PointType} to enter the heist.");
                message = message.Replace("{PointType}", pointType.Name, StringComparison.OrdinalIgnoreCase);
                await ServiceBackbone.SendChatMessage(e.DisplayName, message); //NOTENOUGHPOINTS
                throw new SkipCooldownException();
            }

            if (GameState == State.NotRunning)
            {
                var message = await gameSettingsService.GetStringSetting(GAMENAME, GAMESTARTING, "{Name} is trying to get a team together for some serious heist business! use \"!{Command} AMOUNT/ALL/MAX\" to join!");
                message = message
                    .Replace("{Command}", e.Command, StringComparison.OrdinalIgnoreCase)
                    .Replace("{Name}", e.DisplayName, StringComparison.OrdinalIgnoreCase);
                await ServiceBackbone.SendChatMessage(message); //GAMESTARTING
                GameState = State.Running;
                Entered.Add(new Participant
                {
                    Name = e.Name,
                    DisplayName = e.DisplayName,
                    Bet = amount
                });
                var joinTime = await gameSettingsService.GetIntSetting(GAMENAME, JOINTIME, 300);
                JoinTimer = timeProvider.CreateTimer(JoinTimerCallback, this, TimeSpan.FromSeconds(joinTime), TimeSpan.FromSeconds(joinTime));
            }
            else
            {
                Entered.Add(new Participant
                {
                    Name = e.Name,
                    DisplayName = e.DisplayName,
                    Bet = amount
                });

            }
        }

        private async Task SendInvalidArgsMessage(string displayName, string command)
        {
            var message = await gameSettingsService.GetStringSetting(GAMENAME, INVALIDARGS, "To Enter/Start a heist do !{Command} AMOUNT/ALL/MAX/%");
            message = message.Replace("{Command}", command, StringComparison.OrdinalIgnoreCase);
            await ServiceBackbone.SendChatMessage(displayName, message); //INVALIDARGS
        }

        private async void JoinTimerCallback(object? state)
        {
            if (state == null)
            {
                logger.LogError("State was null, state should never be null!");
                return;
            }

            var heist = (Heist)state;
            await heist.RunStory();
        }

        private async Task RunStory()
        {
            GameState = State.Finishing;
            JoinTimer?.Dispose();
            try
            {
                while (CurrentStoryPart <= 3)
                {
                    switch (CurrentStoryPart)
                    {
                        case 0:
                            CalculateResult();
                            var stageOne = await gameSettingsService.GetStringSetting(GAMENAME, STAGEONE, "The Fin Fam sptvTFF gets ready to steal some pasties from Charlie! sptvCharlie");
                            stageOne = ReplaceStageMessages(stageOne);
                            await ServiceBackbone.SendChatMessage(stageOne); //STAGEONE
                            CurrentStoryPart++;
                            break;

                        case 1:
                            var stageTwo = await gameSettingsService.GetStringSetting(GAMENAME, STAGETWO, "Everyone sharpens their beaks, brushes their feathers, and gets ready to sneak past Charlie!");
                            stageTwo = ReplaceStageMessages(stageTwo);
                            await ServiceBackbone.SendChatMessage(stageTwo); //STAGETWO
                            CurrentStoryPart++;
                            break;

                        case 2:
                            if (Caught.Count > 0)
                            {
                                var stageThree = await gameSettingsService.GetStringSetting(GAMENAME, STAGETHREE, "Look out! Charlie sptvCharlie captured {Caught}");
                                stageThree = ReplaceStageMessages(stageThree);
                                await ServiceBackbone.SendChatMessage(stageThree); //STAGETHREE
                            }
                            CurrentStoryPart++;
                            break;

                        case 3:
                            if (Survivors.Count > 0)
                            {
                                var stageFour = await gameSettingsService.GetStringSetting(GAMENAME, STAGEFOUR, "{Survivors} managed to sneak past Charlie sptvCharlie and grab some of those precious pasties!");
                                stageFour = ReplaceStageMessages(stageFour);
                                await ServiceBackbone.SendChatMessage(stageFour); //STAGEFOUR
                            }
                            CurrentStoryPart++;
                            break;
                    }
                    tools.Sleep(5000);
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed finishing heist");
                await EndHeist();
            }
            await EndHeist();
        }

        private string ReplaceStageMessages(string message)
        {
            return message
                .Replace("{Caught}", GetCaughtNames(), StringComparison.OrdinalIgnoreCase)
                .Replace("{Survivors}", GetWinnerNames(), StringComparison.OrdinalIgnoreCase);
        }

        private async Task EndHeist()
        {
            try
            {
                var maxlength = 0;
                var payouts = new List<string>();
                foreach (var participant in Survivors)
                {
                    var winMultiplier = await gameSettingsService.GetDoubleSetting(GAMENAME, WIN_MULTIPLIER, 1.5);
                    var pay = Convert.ToInt64(participant.Bet * winMultiplier);
                    await pointsSystem.AddPointsByUsernameAndGame(participant.Name, ModuleName, participant.Bet + pay);
                    var formattedName = string.Format("{0} ({1})", participant.DisplayName, (participant.Bet + pay).ToString("N0"));
                    maxlength += formattedName.Length;
                    payouts.Add(formattedName);
                }

                if (payouts.Count == 0)
                {
                    var message = await gameSettingsService.GetStringSetting(GAMENAME, NOONEWON, "The heist ended! There are no survivors.");
                    await ServiceBackbone.SendChatMessage(message); //NOONEWON
                }
                else if (maxlength + 14 > 512)
                {
                    var message = await gameSettingsService.GetStringSetting(GAMENAME, NAMESTOLONG, "The heist ended with {SurvivorsCount} survivor(s) and {CaughtCount} death(s).");
                    message = message
                        .Replace("{SurvivorsCount}", Survivors.Count.ToString(), StringComparison.OrdinalIgnoreCase)
                        .Replace("{CaughtCount}", Caught.Count.ToString(), StringComparison.OrdinalIgnoreCase);
                    await ServiceBackbone.SendChatMessage(message); //HEISTENDEDWITHBOTH
                }
                else
                {
                    var message = await gameSettingsService.GetStringSetting(GAMENAME, SURVIVORS, "The heist ended! Survivors are: {Payouts}.");
                    message = message.Replace("{Payouts}", string.Join(", ", payouts), StringComparison.OrdinalIgnoreCase);
                    await ServiceBackbone.SendChatMessage(message); //HEISTENDED
                }
                await CleanUp();
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed Ending heist");
                await CleanUp();
            }
        }

        private async Task CleanUp()
        {
            try
            {
                Entered.Clear();
                Survivors.Clear();
                Caught.Clear();
                GameState = State.NotRunning;
                CurrentStoryPart = 0;
                var cooldown = await gameSettingsService.GetIntSetting(GAMENAME, COOLDOWN, 300);
                await CommandHandler.AddGlobalCooldown(CommandName, cooldown);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed Cleaning heist");
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
                var result = tools.RandomRange(1, 100);
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

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting {moduledname}", ModuleName);
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopped {moduledname}", ModuleName);
            return Task.CompletedTask;
        }
    }
}