using DotNetTwitchBot.Bot.Commands.Games;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Markov.TokenisationStrategies;
using DotNetTwitchBot.Bot.TwitchServices;
using DotNetTwitchBot.Repository;
using System.Text.RegularExpressions;
using System.Threading;

namespace DotNetTwitchBot.Bot.Commands.Markov
{
    public class MarkovChat(
        ILogger<MarkovChat> logger,
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        IGameSettingsService gameSettingsService,
        IServiceScopeFactory scopeFactory,
        ITwitchService twitchService,
        StringMarkov markov
        ) : BaseCommandService(serviceBackbone, commandHandler, "MarkovChat"), IHostedService, IMarkovChat
    {
        public static readonly string GAMENAME = "MarkovChat";
        public static readonly string EXCLUDE_BOTS = "bots";
        public static readonly string LEVEL = "level";
        public static readonly string NUMBER_OF_MONTHS = "months";

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
                        var message = await markov.Walk(args);
                        await CheckAndSendMessage(message, args);
                    }
                    else
                    {
                        var message = await markov.Walk();
                        await CheckAndSendMessage(message, args);
                    }
                }
            }
        }

        private async Task CheckAndSendMessage(string messages, string args)
        {
            if (!string.IsNullOrWhiteSpace(messages))
            {
                var messageToSend = messages;
                messageToSend = Regex.Replace(messageToSend, @"[^\u0000-\u00FF]+", string.Empty).Trim();
                if (!string.IsNullOrEmpty(messageToSend) && !args.Equals(messageToSend))
                {
                    if (await twitchService.WillBePermittedByAutomod(messageToSend))
                    {
                        await ServiceBackbone.SendChatMessage(messageToSend);
                        return;
                    } else
                    {
                        logger.LogWarning("Message would be automodded generated from Markov: {message}", messageToSend);
                    }
                }
            }
            await ServiceBackbone.SendChatMessage($"I don't know \"{args}\" yet!");
        }

        public override async Task Register()
        {
            await RegisterDefaultCommand("g", this, GAMENAME);
        }

        public async Task LearnMessage(ChatMessageEventArgs e)
        {
            if (markov != null)
            {
                if(e.Message.StartsWith("!") == false 
                    && Bots.Contains(e.Name.ToLower()) == false
                    && e.FromOwnChannel 
                    && e.Message.Contains("http") == false)
                {
                    await markov.Learn([e.Message]);
                }
            }
        }

        public async Task Relearn()
        {
            await UpdateBots();
            await markov.Chain.Clear();
            await Learn();
            
        }

        private async Task Learn()
        {
            var numberOfMonths = await gameSettingsService.GetIntSetting(GAMENAME, NUMBER_OF_MONTHS, 3);
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var bannedUsers = db.BannedViewers.GetAll().Select(x => x.Username.ToLower()).ToList();
            var messages = await db.ViewerChatHistories
                .Find(x => x.Message.StartsWith("!") == false &&
                Bots.Contains(x.Username.ToLower()) == false &&
                bannedUsers.Contains(x.Username.ToLower()) == false && 
                x.Message.Contains("http") == false &&
                x.CreatedAt > DateTime.Now.AddMonths(-numberOfMonths))
                .Select(x => x.Message).ToListAsync();

            markov.Level = await gameSettingsService.GetIntSetting(GAMENAME, LEVEL, 2);

            if (messages != null)
            {
                await markov.Learn(messages);
            }
            else
            {
                logger.LogWarning("No messages found to teach MarkovChat");
            }
            logger.LogInformation("MarkovChat is ready");
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

            await Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
