using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class Roll : BaseCommandService, IHostedService
    {
        private readonly ILogger<Roll> _logger;
        private readonly ILoyaltyFeature _loyaltyFeature;
        private readonly List<string> WinMessages = LoadWinMessages();
        private readonly List<string> LostMessages = LoadLostMessages();
        private readonly List<int> prizes = new()
        {
            40, 160,360,
            640, 1000, 1440
        };

        public Roll(
            ILogger<Roll> logger,
            ILoyaltyFeature loyaltyFeature,
            IServiceBackbone serviceBackbone,
            ICommandHandler commandHandler
            ) : base(serviceBackbone, commandHandler, "Roll")
        {
            _logger = logger;
            _loyaltyFeature = loyaltyFeature;
        }

        public override async Task Register()
        {
            var moduleName = "Roll";
            await RegisterDefaultCommand("roll", this, moduleName, Rank.Viewer, userCooldown: 180, sayCooldown: false);
            await RegisterDefaultCommand("dice", this, moduleName, Rank.Viewer, userCooldown: 180, sayCooldown: false);
            _logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            switch (command.CommandProperties.CommandName)
            {
                case "roll":
                case "dice":
                    try
                    {
                        await RunGame(e);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error running dice game");
                    }
                    break;
            }
        }

        private async Task RunGame(CommandEventArgs e)
        {
            var dice1 = Tools.RandomRange(1, 6);
            var dice2 = Tools.RandomRange(1, 6);
            var resultMessage = string.Format("{0} rolls a [{1}] and [{2}]. ", e.DisplayName, dice1, dice2);
            if (dice1 == dice2)
            {
                switch (dice1)
                {
                    case 1:
                        resultMessage += string.Format("Snake eyes for {0} pasties! ", prizes[0]);
                        break;
                    case 2:
                        resultMessage += string.Format("Hard four for {0} pasties! ", prizes[1]);
                        break;
                    case 3:
                        resultMessage += string.Format("Hard six for {0} pasties! ", prizes[2]);
                        break;
                    case 4:
                        resultMessage += string.Format("Hard eight for {0} pasties! ", prizes[3]);
                        break;
                    case 5:
                        resultMessage += string.Format("Hard ten for {0} pasties! ", prizes[4]);
                        break;
                    case 6:
                        resultMessage += string.Format("Boxcars to the max!!! {0} pasties! ", prizes[5]);
                        break;
                }
                var winMessage = string.Format(WinMessages.RandomElement(), e.DisplayName);
                await ServiceBackbone.SendChatMessage(resultMessage + winMessage);
                await _loyaltyFeature.AddPointsToViewer(e.Name, prizes[dice1 - 1]);
            }
            else
            {
                var lostMessage = string.Format(LostMessages.RandomElement(), e.DisplayName);
                await ServiceBackbone.SendChatMessage(resultMessage + lostMessage);
            }

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
                "If there were more clumsy and perverted people like {0}, the world would be a better place.",
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

        private static List<string> LoadLostMessages()
        {
            return new List<string>{
                "Better luck next time!",
                "Gambling can be hard, but don\'t stray.",
                "Dreamin\', don\'t give it up {0}",
                "You have ignited a nuclear war! And no, there is no animated display of a mushroom cloud. Why? Because we do not reward failure.",
                "Can you like.. win? please?",
                "Game Over.",
                "Don\'t looooose your waaaaaaay!",
                "You just weren\'t good enough.",
                "Will {0} finally win? Find out next time on Dragon Ball Z!",
                "{0} has lost something great today!",
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
                "You with the keyboard! I won DESPITE you. You suck. And smell -- REALLY smell.",
                "A loser doesn\'t know what he\'ll do if he loses, but talks about what he\'ll do if he wins, and a winner doesn\'t talk about what he\'ll do if he wins, but knows what he\'ll do if he loses.",
                "If you can\'t win, lose like a champion!",
                "Welcome to Loserville! Population: You!",
                "Whoever said, \"It's not whether you win or lose that counts,\" probably lost.",
                "What went wrong? What didn\'t? - it was just one of those days. Not your day really"
            };
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopped {moduledname}", ModuleName);
            return Task.CompletedTask;
        }
    }
}