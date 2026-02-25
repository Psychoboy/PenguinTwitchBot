using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Commands.Games;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Events.Chat;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class Slots(
        ILogger<Slots> logger,
        IPointsSystem pointsSystem,
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        MaxBetCalculator maxBetCalculator,
        IMediator mediator,
        IGameSettingsService gameSettingsService
            ) : BaseCommandService(serviceBackbone, commandHandler, GAMENAME, mediator), IHostedService
    {
        private List<string> Emotes = [];
        private readonly List<Int64> Prizes = LoadPrizes();
        private readonly List<string> WinMessages = LoadWinMessages();
        private readonly List<string> LoseMessages = LoadLoseMessages();

        public static readonly string GAMESETTING_3_OF_A_KIND = "3 of a kind";
        public static readonly string GAMESETTING_2_OF_A_KIND = "2 of a kind";
        public static readonly string GAMESETTING_FIRST_2_MULTIPLIER = "First 2 multiplier";
        public static readonly string GAMESETTING_LAST_2_MULTIPLIER = "Last 2 multiplier";
        public static readonly string GAMESETTING_EMOTES = "Emotes";
        public static readonly string GAMENAME = "Slots";

        public override async Task Register()
        {
            var moduleName = "Slots";
            await RegisterDefaultCommand("slot", this, moduleName, Rank.Viewer, userCooldown: 600);
            await RegisterDefaultCommand("slots", this, moduleName, Rank.Viewer, userCooldown: 600);
            await pointsSystem.RegisterDefaultPointForGame(ModuleName);
            logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            switch (command.CommandProperties.CommandName)
            {
                case "slot":
                case "slots":
                    await CalculateResult(e);
                    break;
            }
        }

        private async Task CalculateResult(CommandEventArgs e)
        {
            var amount = await CheckBetAmount(e);

            var e1 = GetEmoteKey();
            var e2 = GetEmoteKey();
            var e3 = GetEmoteKey();
            var message = string.Format("{0}: [ {1} {2} {3} ] ", e.DisplayName, Emotes[e1], Emotes[e2], Emotes[e3]);

            if (e1 == e2 && e2 == e3) // 3 of a kind
            {
                var prizeWinnings = Prizes[e1];
                if (amount > 0)
                {
                    prizeWinnings = Convert.ToInt64(amount * await gameSettingsService.GetDoubleSetting(ModuleName, GAMESETTING_3_OF_A_KIND, 3.5));
                }

                message += string.Format("{0} pasties. ", prizeWinnings.ToString("N0"));
                var randomMessage = WinMessages[StaticTools.Next(0, WinMessages.Count)];
                message += randomMessage.Replace("{NAME_HERE}", e.DisplayName);

                await ServiceBackbone.SendChatMessage(message, e.Platform);
                await pointsSystem.AddPointsByUserIdAndGame(e.UserId, e.Platform, ModuleName, prizeWinnings);
                return;
            }
            else if (e1 == e2 || e2 == e3 || e3 == e1) // 2 of a kind
            {
                var twoOfAKindMultiplier = await gameSettingsService.GetDoubleSetting(ModuleName, GAMESETTING_2_OF_A_KIND, 0.3);
                var prizeWinnings = e1 == e2 ? Convert.ToInt64(Prizes[e1] * twoOfAKindMultiplier) : Convert.ToInt64(Prizes[e3] * twoOfAKindMultiplier);
                if (amount > 0)
                {
                    prizeWinnings = e1 == e2 ? 
                        Convert.ToInt64(amount * await gameSettingsService.GetDoubleSetting(ModuleName, GAMESETTING_FIRST_2_MULTIPLIER, 2.5)) : 
                        Convert.ToInt64(amount * await gameSettingsService.GetDoubleSetting(ModuleName, GAMESETTING_LAST_2_MULTIPLIER, 1.5));
                }

                message += string.Format("{0} pasties. ", prizeWinnings.ToString("N0"));
                var randomMessage = WinMessages[StaticTools.Next(0, WinMessages.Count)];
                message += randomMessage.Replace("{NAME_HERE}", e.DisplayName);

                await ServiceBackbone.SendChatMessage(message, e.Platform);
                await pointsSystem.AddPointsByUserIdAndGame(e.UserId, e.Platform, ModuleName, prizeWinnings);
                return;
            }
            var randomLoseMessage = LoseMessages[StaticTools.Next(0, WinMessages.Count)];
            //{NAME_HERE}
            message += randomLoseMessage;
            await ServiceBackbone.SendChatMessage(message.Replace("{NAME_HERE}", e.DisplayName), e.Platform);
        }

        private async Task<long> CheckBetAmount(CommandEventArgs e)
        {
            if (e.Args.Count == 0)
            {
                return 0;
            }

            var maxBet = await maxBetCalculator.CheckAndRemovePoints(e.UserId, e.Platform, "slots", e.Args.First(), 25);
            switch (maxBet.Result)
            {
                case MaxBet.ParseResult.Success:
                    return maxBet.Amount;
                case MaxBet.ParseResult.InvalidValue:
                    await ServiceBackbone.ResponseWithMessage(e,
                        "To use !slots, either just run !slots or do !slots max or all or amount or % (ie. 50%)");
                    throw new SkipCooldownException();
                case MaxBet.ParseResult.ToMuch:
                case MaxBet.ParseResult.ToLow:
                    await ServiceBackbone.ResponseWithMessage(e,
                        string.Format("The max bet is {0} and must be greater then 25", LoyaltyFeature.MaxBet.ToString("N0")));
                    throw new SkipCooldownException();
                case MaxBet.ParseResult.NotEnough:
                    await ServiceBackbone.ResponseWithMessage(e,
                        "you don't have that much to play slots with.");
                    throw new SkipCooldownException();
            }
            return 0;
        }

        private static List<long> LoadPrizes()
        {
            var prizes = new List<long>{
                50,
                75,
                100,
                250,
                400,
                500,
                600,
                650,
                700,
                800,
                900,
                1000
            };
            prizes.Reverse();
            return prizes;
        }

        public async Task LoadEmotes()
        {
            Emotes = await gameSettingsService.GetStringListSetting(ModuleName, GAMESETTING_EMOTES, GetDefaultEmotes());
        }

        public static List<string> GetDefaultEmotes()
        {
            return [
                //"sptvLove",
                "sptvDrink",
                "sptvBacon",
                "sptvWaffle",
                "sptvPancake",
                "sptvLights",
                //"sptvSalute",
                //"sptvHype",
                //"sptvHello",
                //"sptv30k",
                //"sptvHi"
            ];
        }

        private int GetEmoteKey()
        {
            var key = StaticTools.Next(0, Emotes.Count);
            return key;
        }

        private static List<string> LoadWinMessages()
        {
            return new List<string>
            {
                "Congratulations!",
                "On a scale of 1 to 10, this was 2 easy",
                "Aw, yeah!",
                "You got lucky.",
                "GOOOOOOOAAAAAL!!",
                "Keep it up!",
                "Baby, now you\'re number one, shining bright for everyone!",
                "I only let you win out of pity.",
                "If there were more clumsy and perverted people like {NAME_HERE}, the world would be a better place.",
                "Dreams do come true!",
                "The way to success is always difficult, but you still manage to get yourself on top and be honored.",
                "You rarely win, but sometimes you do.",
                "Sometimes in life you don\'t always feel like a winner, but that doesn\'t mean you\'re not a winner.",
                "It\'s easy to win. Anybody can win.",
                "Winning is great, sure, but if you are really going to do something in life, the secret is learning how to lose.",
                "A winner is just a loser who tried one more time.",
                "Sugoi~!",
                "This thing must have been rigged!",
                "The Goddess Fortuna smiles upon you.",
                "?!......... (Seriously?!)"
            };
        }

        private static List<string> LoadLoseMessages()
        {
            return new List<string>{
                "Better luck next time!",
                "Gambling can be hard, but don\'t stray.",
                "Dreamin\', don\'t give it up {NAME_HERE}",
                "You have ignited a nuclear war! And no, there is no animated display of a mushroom cloud. Why? Because we do not reward failure.",
                "Can you like.. win? please?",
                "Game Over.",
                "Don\'t looooose your waaaaaaay!",
                "You just weren\'t good enough.",
                "Will {NAME_HERE} finally win? Find out next time on Dragon Ball Z!",
                "{NAME_HERE} has lost something great today!",
                "Perhaps if you trained in the mountains in solitude, you could learn how to win.",
                "Believe in the heart of the cards!",
                "Believe in me who believes in you!",
                "404 Win Not Found.",
                "If the human body is 65% water, how can you be 100% salt?",
                "To win you must gain sight beyond sight!",
                "You\'re great at losing! Don\'t let anyone tell you otherwise.",
                "So tell me, what\'s it like living in a constant haze of losses?",
                "Did you know that games of chance is the same way how Quantum Mechanics work?",
                "L-O-S-E-R...",
                "Dreams shattered :(",
                "Looks like you\'ve activated my trap card Kappa",
                "You\'re not obligated to win. You\'re obligated to keep trying.",
                "This is not the end, this is not even the beginning of the end, this is just perhaps the end of the beginning.",
                "Sometimes not getting what you want is a brilliant stroke of luck.",
                "Winning takes talent, to repeat takes character.",
                "Snake? Snake?! Snaaaaaaaaaake!!.",
                "You\'re like forrest gump without the running thing",
                "You should go set the world record for holding your breath.",
                "Shut down!",
                "You don\'t deserve to play this game. Go back to playing with crayons.",
                "Too bad. Game over. Insert new quarter.",
                "The Goddess Fortuna frowns upon you.",
                "In my ideal nation, there would exist no one as weak as you!",
                "What a joke!",
                "Learn from your defeat, child.",
                "You do not have enough experience! Are you listening to me?!",
                "Hope you\'re listening. Level up homie!",
                "Your technique need work.",
                "Hey, did you hurt yourself?",
                "That\'s your best?",
                "Hah ha ha ha ha ha ha!",
                "Don\'t make excuses for your loss! Go train and try again!",
                "Hey, you\'re not that bad. You\'re not very good, either.",
                "I\'ve had meetings that were more grueling than this.",
                "Even by the lowest standards, that was really bad.",
                "Ha-ha-ha! What\'s the matter? You don\'t like losing? Well, that\'s not my problem. Ha, ha, ha, ha, ha!",
                "Ancient words of wisdom... \"you suck\"",
                "I won\'t say you\'re bad. I\'ll just think it, OK?",
                "I think I\'ve learned something from this. You\'re nothing...",
                "Hey! Don\'t worry about it! You know... being bad and all!",
                "The tragedy of all losers is that they think they were on the verge of victory.",
                "Hm! You should go back to playing puzzle games!",
                "The past is the past man. If you are a loser now, then you’re a loser period.",
                "The reason you lost is quite simple: you\'re bad!",
                "Remember that one time during the game when it looked like you might actually win? No? Me neither.",
                "Is that all you can do? You wouldn\'t have gone very far with that anyway.",
                "Don\'t blame bad luck or fate. You lost because you suck.",
                "You weren\'t that bad. You were pathetic! Go home!",
                "You made an effort at least, pathetic as it was!",
                "My dad could win, and he\'s dead!",
                "You with the keyboard! I won DESPITE you. You suck. And smell -- REALLY smell."
            };
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting {moduledname}", ModuleName);
            await Register();
            await LoadEmotes();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopped {moduledname}", ModuleName);
            return Task.CompletedTask;
        }
    }
}