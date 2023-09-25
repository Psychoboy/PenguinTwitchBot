using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class First : BaseCommandService
    {
        private List<string> ClaimedFirst { get; } = new List<string>();
        private readonly int MaxClaims = 60;
        private readonly ITicketsFeature _ticketsFeature;
        private readonly ILogger<First> _logger;

        public First(
            IServiceBackbone eventService,
            ILogger<First> logger,
            ITicketsFeature ticketsFeature,
            ICommandHandler commandHandler
        ) : base(eventService, commandHandler)
        {
            _ticketsFeature = ticketsFeature;
            _logger = logger;
        }

        public static int CurrentClaims { get; set; } = 0;

        public override async Task Register()
        {
            var moduleName = "First";
            await RegisterDefaultCommand("first", this, moduleName);
            await RegisterDefaultCommand("resetfirst", this, moduleName, Rank.Streamer);
            _logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommandDefaultName(e.Command);
            switch (command)
            {
                case "first":
                    {
                        await GiveFirst(e.Name);

                    }
                    break;
                case "resetfirst":
                    {
                        ResetFirst();
                    }
                    break;
            }
        }

        private void ResetFirst()
        {
            ClaimedFirst.Clear();
            CurrentClaims = 0;
        }

        private async Task GiveFirst(string sender)
        {
            if (!ServiceBackbone.IsOnline)
            {
                await SendChatMessage(sender, "Nice try, the stream is currently offline.");
                throw new SkipCooldownException();
            }

            if (ClaimedFirst.Count >= MaxClaims)
            {
                await SendChatMessage(sender, "Sorry, You were to slow today. FeelsBadMan");
                throw new SkipCooldownException();
            }

            if (ClaimedFirst.Contains(sender.ToLower())) throw new SkipCooldownException();

            ClaimedFirst.Add(sender.ToLower());
            var awardPoints = (int)Math.Floor(((double)MaxClaims - CurrentClaims) / 2);
            if (awardPoints == 0)
            {
                await SendChatMessage(sender, "Sorry, You were to slow today. FeelsBadMan");
                throw new SkipCooldownException();
            }
            CurrentClaims++;
            _logger.LogInformation("Current Claims: {CurrentClaims}", CurrentClaims);
            await _ticketsFeature.GiveTicketsToViewer(sender, awardPoints);
            await SendChatMessage(sender, string.Format("Whooohooo! You came in position {0} and get {1} tickets!! PogChamp", ClaimedFirst.Count, awardPoints));
        }
    }
}