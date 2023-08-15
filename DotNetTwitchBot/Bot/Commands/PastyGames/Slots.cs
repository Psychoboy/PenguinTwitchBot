using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class Slots : BaseCommandService
    {
        private readonly LoyaltyFeature _loyaltyFeature;
        private readonly ILogger<Slots> _logger;
        private readonly List<string> Emotes = LoadEmotes();
        private readonly List<Int64> Prizes = LoadPrizes();
        private readonly List<string> WinMessages = LoadWinMessages();
        private readonly List<string> LoseMessages = LoadLoseMessages();

        public Slots(
            ILogger<Slots> logger,
            LoyaltyFeature loyaltyService,
            IServiceBackbone serviceBackbone,
            ICommandHandler commandHandler
            ) : base(serviceBackbone, commandHandler)
        {
            _loyaltyFeature = loyaltyService;
            _logger = logger;
        }

        public override async Task Register()
        {
            var moduleName = "Slots";
            await RegisterDefaultCommand("slot", this, moduleName, Rank.Viewer, userCooldown: 600);
            await RegisterDefaultCommand("slots", this, moduleName, Rank.Viewer, userCooldown: 600);
            _logger.LogInformation($"Registered commands for {moduleName}");
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

        private int GetEmoteKey()
        {
            var key = Tools.Next(1, 1000);
            return key switch
            {
                <= 75 => 4,
                > 75 and <= 200 => 3,
                > 200 and <= 450 => 2,
                > 450 and <= 700 => 1,
                _ => 0,
            };
        }

        private async Task CalculateResult(CommandEventArgs e)
        {
            var e1 = GetEmoteKey();
            var e2 = GetEmoteKey();
            var e3 = GetEmoteKey();
            var message = string.Format("{0}: [ {1} {2} {3} ] ", e.DisplayName, Emotes[e1], Emotes[e2], Emotes[e3]);

            if (e1 == e2 && e2 == e3)
            {
                message += string.Format("+{0} pasties. ", Prizes[e1]);
                var randomMessage = WinMessages[Tools.Next(0, WinMessages.Count)];
                message += string.Format(randomMessage, e.DisplayName);
                await ServiceBackbone.SendChatMessage(message);
                await _loyaltyFeature.AddPointsToViewer(e.Name, Prizes[e1]);
                return;
            }
            else if (e1 == e2 || e2 == e3 || e3 == e1)
            {
                var prize = e1 == e2 ? Convert.ToInt64(Prizes[e1] * 0.3) : Convert.ToInt64(Prizes[e3] * 0.3);
                message += string.Format("+{0} pasties. ", prize);
                var randomMessage = WinMessages[Tools.Next(0, WinMessages.Count)];
                message += string.Format(randomMessage, e.DisplayName);
                await ServiceBackbone.SendChatMessage(message);
                await _loyaltyFeature.AddPointsToViewer(e.Name, prize);
                return;
            }
            var randomLoseMessage = LoseMessages[Tools.Next(0, WinMessages.Count)];
            //{NAME_HERE}
            message += randomLoseMessage;
            await ServiceBackbone.SendChatMessage(message.Replace("{NAME_HERE}", e.DisplayName));
        }

        private static List<long> LoadPrizes()
        {
            return new List<long>{
                75,
                150,
                300,
                450,
                1000
            };
        }

        private static List<string> LoadEmotes()
        {
            return new List<string>{
                "sptvLove",
                "sptvDrink",
                "sptvBacon",
                "sptvWaffle",
                "sptvPancake"
            };
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
    }
}