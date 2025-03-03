using DotNetTwitchBot.Bot.Commands.Games;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Markov.TokenisationStrategies;
using DotNetTwitchBot.Repository;
using System.Text.RegularExpressions;

namespace DotNetTwitchBot.Bot.Commands.Markov
{
    public class MarkovChat(
        ILogger<MarkovChat> logger,
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        IGameSettingsService gameSettingsService,
        IServiceScopeFactory scopeFactory,
        StringMarkov markov
        ) : BaseCommandService(serviceBackbone, commandHandler, "MarkovChat"), IHostedService, IMarkovChat
    {
        public static readonly string GAMENAME = "MarkovChat";
        public static readonly string EXCLUDE_BOTS = "bots";

        //private StringMarkov? markov;
        private List<string> Bots = [];
        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommandDefaultName(e.Command);
            if (command == "g")
            {
                if (markov != null)
                {
                    var args = e.Arg;
                    args = Regex.Replace(args, @"[^\u0000-\u00FF]+", string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(args))
                    {
                        var message = markov.Walk(1, e.Args.First());
                        await CheckAndSendMessage(message, args);
                    }
                    else
                    {
                        var message = markov.Walk(1);
                        await CheckAndSendMessage(message, args);
                    }
                }
            }
        }

        private Task CheckAndSendMessage(IEnumerable<string>? messages, string args)
        {
            if (messages != null && messages.Any())
            {
                var messageToSend = messages.First();
                messageToSend = Regex.Replace(messageToSend, @"[^\u0000-\u00FF]+", string.Empty).Trim();
                if (!string.IsNullOrEmpty(messageToSend) && !args.Equals(messageToSend))
                {
                    return ServiceBackbone.SendChatMessage(messageToSend);
                }
            }
            return ServiceBackbone.SendChatMessage($"I don't know \"{args}\" yet!");
        }

        public override async Task Register()
        {
            await RegisterDefaultCommand("g", this, GAMENAME);
        }

        public void LearnMessage(ChatMessageEventArgs e)
        {
            if (markov != null)
            {
                if(e.Message.StartsWith("!") == false 
                    && Bots.Contains(e.Name.ToLower()) == false)
                {
                    markov.Learn([e.Message], false);
                }
            }
        }

        public async Task UpdateBots()
        {
            Bots = await gameSettingsService.GetStringListSetting(GAMENAME, EXCLUDE_BOTS, ["streamelements",
                "streamlabs",
                "nightbot",
                "moobot",
                "ankhbot",
                "phantombot",
                "wizebot",
                "super_waffle_bot",
                "defbott",
                "drinking_buddy_bot",
                "dixperbot",
                "lumiastream"]);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting MarkovChat");
            logger.LogInformation("Teaching MarkovChat");
            await UpdateBots();


            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var bannedUsers = db.BannedViewers.GetAll().Select(x => x.Username.ToLower()).ToList();
            var messages = await db.ViewerChatHistories
                .Find(x => x.Message.StartsWith("!") == false &&
                Bots.Contains(x.Username.ToLower()) == false &&
                bannedUsers.Contains(x.Username.ToLower()) == false)
                .Select(x => x.Message).ToListAsync(cancellationToken);
            if (messages != null)
            {
                markov.Learn(messages, false);
            }
            else
            {
                logger.LogWarning("No messages found to teach MarkovChat");
            }
            logger.LogInformation("MarkovChat is ready");

            await Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
