using DotNetTwitchBot.Bot.Actions;
using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using DotNetTwitchBot.Bot.Commands.Custom.Tags.PlayerSound;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Commands.Misc;
using DotNetTwitchBot.Bot.Commands.TTS;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models.Commands;
using DotNetTwitchBot.Bot.TwitchServices;
using DotNetTwitchBot.Repository;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DotNetTwitchBot.Bot.Commands.Custom
{
    public partial class CustomCommand : BaseCommandService, IHostedService
    {
        [GeneratedRegex(
            @"(?:[^\\]|^)(\(([^\\\s\|=()]*)([\s=\|](?:\\\(|\\\)|[^()])*)?\))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase)]
        private static partial Regex MatchTagNames();
        [GeneratedRegex(
            @"\\([\\()])", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
        private static partial Regex UnEscapedRegex();

        [GeneratedRegex(
            @"^(\S+)(?:\s(.*))?$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
        public static partial Regex CounterRegex();

        readonly List<string> CommandTags = [];
        static readonly SemaphoreSlim _semaphoreSlim = new(1);
        List<KeywordWithRegex> Keywords = [];
        private readonly Application.Notifications.IPenguinDispatcher _dispatcher;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CustomCommand> _logger;
        private readonly ITwitchService _twitchService;
        private readonly IPointsSystem _PointsSystem;

        public CustomCommand(
            Application.Notifications.IPenguinDispatcher dispatcher,
            IViewerFeature viewerFeature,
            IServiceScopeFactory scopeFactory,
            ILogger<CustomCommand> logger,
            ITwitchService twitchService,
            IPointsSystem pointsSystem,
            IServiceBackbone serviceBackbone,
            ICommandHandler commandHandler) : base(serviceBackbone, commandHandler, "CustomCommands", dispatcher)
        {
            _dispatcher = dispatcher;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _twitchService = twitchService;
            _PointsSystem = pointsSystem;

            //Register Tags here
            CommandTags.Add("alert");
            CommandTags.Add("playsound");
            CommandTags.Add("sender");
            CommandTags.Add("args");
            CommandTags.Add("randomint");
            CommandTags.Add("useronly");
            CommandTags.Add("writefile");
            CommandTags.Add("currenttime");
            CommandTags.Add("@sender");
            CommandTags.Add("onlineonly");
            CommandTags.Add("offlineonly");
            CommandTags.Add("followage");
            CommandTags.Add("multicounter");
            CommandTags.Add("multicounteralert");
            CommandTags.Add("uptime");
            CommandTags.Add("customapitext");
            CommandTags.Add("customapinoresponse");
            CommandTags.Add("GiveawayPrize");
            CommandTags.Add("target");
            CommandTags.Add("targetorself");
            CommandTags.Add("watchtime");
            CommandTags.Add("command");
            CommandTags.Add("elevatedcommand");
            CommandTags.Add("ttsandprint");
            CommandTags.Add("tts");
            CommandTags.Add("enablechannelpoint");
            CommandTags.Add("disablechannelpoint");
            CommandTags.Add("pausechannelpoint");
            CommandTags.Add("unpausechannelpoint");
            CommandTags.Add("enabletimergroup");
            CommandTags.Add("disabletimergroup");
            CommandTags.Add("enablecommand");
            CommandTags.Add("disablecommand");
            CommandTags.Add("giftpoints");
            CommandTags.Add("checkpoints");
        }

        public List<KeywordType> GetKeywords()
        {
            return Keywords.Select(x => x.Keyword).ToList();
        }


        public async Task<KeywordType?> GetKeyword(int id)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.Keywords.Find(x => x.Id == id).FirstOrDefaultAsync();
        }



        private async Task LoadCommands()
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                _logger.LogInformation("Loading keywords");
                Keywords.Clear();
                Keywords = (await db.Keywords.GetAllAsync()).Select(x => new KeywordWithRegex(x)).ToList();
            }

            foreach (var keyword in Keywords)
            {
                if (keyword.Keyword.IsRegex)
                {
                    keyword.Regex = new Regex(keyword.Keyword.CommandName, RegexOptions.None, TimeSpan.FromMilliseconds(500));
                }
            }
        }

        public async Task AddKeyword(KeywordType keyword)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                if ((await db.Keywords.Find(x => x.CommandName.Equals(keyword.CommandName)).FirstOrDefaultAsync()) != null)
                {
                    _logger.LogWarning("Keyword already exists");
                    return;
                }
                await db.Keywords.AddAsync(keyword);
                await db.SaveChangesAsync();
            }
            await LoadCommands();
        }

        public async Task DeleteKeyword(KeywordType keyword)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                db.Keywords.Remove(keyword);
                await db.SaveChangesAsync();
            }
            await LoadCommands();
        }

        public async Task SaveKeyword(KeywordType keyword)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                db.Keywords.Update(keyword);
                await db.SaveChangesAsync();
            }
            await LoadCommands();
        }

        //To check for keywords
        public async Task ReceivedChatMessage(ChatMessageEventArgs e)
        {
            if (e.Message.StartsWith("!")) return; //Ignore commands
            bool match = false;
            foreach (var keyword in Keywords.ToList())
            {
                if (await CommandHandler.IsCoolDownExpired(e.Name, "keyword " + keyword.Keyword.CommandName) == false) continue;
                if (keyword.Keyword.IsRegex)
                {
                    if (keyword.Regex.IsMatch(e.Message)) match = true;
                }
                else
                {
                    if (keyword.Keyword.IsCaseSensitive)
                    {
                        if (e.Message.Contains(keyword.Keyword.CommandName, StringComparison.OrdinalIgnoreCase)) match = true;
                    }
                    else
                    {
                        if (e.Message.Contains(keyword.Keyword.CommandName, StringComparison.OrdinalIgnoreCase)) match = true;
                    }

                }
                if (match)
                {
                    var commandEventArgs = new CommandEventArgs
                    {
                        Arg = e.Message,
                        Args = [.. e.Message.Split(" ")],
                        IsWhisper = false,
                        Name = e.Name,
                        DisplayName = e.DisplayName,
                        IsSub = e.IsSub,
                        IsMod = e.IsBroadcaster || e.IsMod,
                        IsVip = e.IsVip,
                        IsBroadcaster = e.IsBroadcaster,
                    };

                    if(await CommandHandler.CheckPermission(keyword.Keyword, commandEventArgs) == false)
                    {
                        return;
                    }

                    await ProcessTagsAndSayMessage(commandEventArgs, keyword.Keyword.Response, false, keyword.Keyword.SourceOnly);

                    if (keyword.Keyword.GlobalCooldown > 0)
                    {
                        await CommandHandler.AddGlobalCooldown("keyword " + keyword.Keyword.CommandName, keyword.Keyword.GlobalCooldown);
                    }
                    if (keyword.Keyword.UserCooldown > 0)
                    {
                        await CommandHandler.AddCoolDown(e.Name, "keyword " + keyword.Keyword.CommandName, keyword.Keyword.UserCooldown);
                    }
                    break;
                }
            }

        }

        public override async Task Register()
        {
            var moduleName = "CustomCommands";
            await RegisterDefaultCommand("addcommand", this, moduleName, Rank.Streamer);
            await RegisterDefaultCommand("refreshcommands", this, moduleName, Rank.Streamer);
            await RegisterDefaultCommand("disablecommand", this, moduleName, Rank.Streamer);
            await RegisterDefaultCommand("enablecommand", this, moduleName, Rank.Streamer);
            _logger.LogInformation("Registered commands for {moduleName}", moduleName);
            await LoadCommands();
        }

        public override Task OnCommand(object? sender, CommandEventArgs e)
        {
            return Task.CompletedTask; //Nothing to do anymore
        }

        public async Task<CustomCommandResult> ProcessTags(CommandEventArgs eventArgs, string originalText)
        {
            try
            {
                var message = originalText;

                if (string.IsNullOrWhiteSpace(message)) return new CustomCommandResult();
                var cancel = false;
                var replyToMessage = false;
                while (true)
                {
                    var matches = MatchTagNames().Matches(message);
                    if (matches.Count == 0) break;
                    foreach (Match match in matches.Cast<Match>())
                    {
                        bool thisTagFound = false;
                        var groups = match.Groups;
                        var wholeMatch = groups[1];
                        var tagName = groups[2];
                        var tagArgs = groups[3];

                        if (CommandTags.Contains(tagName.Value.Trim()))
                        {
                            thisTagFound = true;
                            CustomCommandResult result = await GetResult(tagName.Value.Trim(), eventArgs, tagArgs.Value.Trim());
                            if (result.Cancel)
                            {
                                cancel = true;
                                break;
                            }

                            if (result.ReplyToMessage)
                            {
                                replyToMessage = true;
                            }

                            message = ReplaceFirstOccurrence(message, wholeMatch.Value, result.Message);
                        }
                        if (!thisTagFound)
                        {
                            message = message.Replace(wholeMatch.Value, "\\(" + wholeMatch.Value[1..^1] + "\\)");
                        }

                    }
                    if (cancel) return new CustomCommandResult(cancel);
                }
                return new CustomCommandResult(message, cancel, replyToMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running custom command.");
                return new CustomCommandResult(true);
            }
        }

        private Task<CustomCommandResult> GetResult(string tagName, CommandEventArgs eventArgs, string args)
        {
            return tagName.ToLower() switch
            {
                "alert" => _dispatcher.Send(new AlertTag { CommandEventArgs = eventArgs, Args = args }),//
                "playsound" => _dispatcher.Send(new PlaySoundTag { CommandEventArgs = eventArgs, Args = args }),//
                "sender" => _dispatcher.Send(new SenderTag { CommandEventArgs = eventArgs, Args = args }),
                "args" => _dispatcher.Send(new ArgsTag { CommandEventArgs = eventArgs, Args = args }),
                "randomint" => _dispatcher.Send(new RandomIntTag { CommandEventArgs = eventArgs, Args = args }),//
                "useronly" => _dispatcher.Send(new UserOnlyTag { CommandEventArgs = eventArgs, Args = args }),
                "writefile" => _dispatcher.Send(new WriteFileTag { CommandEventArgs = eventArgs, Args = args }),//
                "currenttime" => _dispatcher.Send(new CurrentTimeTag { CommandEventArgs = eventArgs, Args = args }),//
                "@sender" => _dispatcher.Send(new AtSenderTag { CommandEventArgs = eventArgs, Args = args }),
                "onlineonly" => _dispatcher.Send(new OnlineOnlyTag { CommandEventArgs = eventArgs, Args = args }),
                "offlineonly" => _dispatcher.Send(new OfflineOnlyTag { CommandEventArgs = eventArgs, Args = args }),
                "followage" => _dispatcher.Send(new FollowAgeTag { CommandEventArgs = eventArgs, Args = args }), //
                "multicounter" => _dispatcher.Send(new MultiCounterTag { CommandEventArgs = eventArgs, Args = args }),
                "multicounteralert" => _dispatcher.Send(new MultiCounterAlertTag { CommandEventArgs = eventArgs, Args = args }),
                "uptime" => _dispatcher.Send(new UptimeTag { CommandEventArgs = eventArgs, Args = args }), //
                "customapitext" => _dispatcher.Send(new CustomApiTextTag { CommandEventArgs = eventArgs, Args = args }),
                "customapinoresponse" => _dispatcher.Send(new CustomApiNoResponseTag { CommandEventArgs = eventArgs, Args = args }), //
                "giveawayprize" => _dispatcher.Send(new GiveawayPrizeTag { CommandEventArgs = eventArgs, Args = args }), //
                "target" => _dispatcher.Send(new TargetTag { CommandEventArgs = eventArgs, Args = args }),//--
                "targetorself" => _dispatcher.Send(new TargetOrSelfTag { CommandEventArgs = eventArgs, Args = args }),//--
                "watchtime" => _dispatcher.Send(new WatchTimeTag { CommandEventArgs = eventArgs, Args = args }), //
                "command" => _dispatcher.Send(new ExecuteCommandTag { CommandEventArgs = eventArgs, Args = args }),
                "elevatedcommand" => _dispatcher.Send(new ExecuteElevatedCommandTag { CommandEventArgs = eventArgs, Args = args }),
                "ttsandprint" => _dispatcher.Send(new TTSAndPrintTag { CommandEventArgs = eventArgs, Args = args }),
                "tts" => _dispatcher.Send(new TTSTag { CommandEventArgs = eventArgs, Args = args }),
                "enablechannelpoint" => _dispatcher.Send(new EnableChannelPointTag { CommandEventArgs = eventArgs, Args = args }),
                "disablechannelpoint" => _dispatcher.Send(new DisableChannelPointTag { CommandEventArgs = eventArgs, Args = args }),
                "pausechannelpoint" => _dispatcher.Send(new PauseChannelPointTag { CommandEventArgs = eventArgs, Args = args }),
                "unpausechannelpoint" => _dispatcher.Send(new UnpauseChannelPointTag { CommandEventArgs = eventArgs, Args = args }),
                "giftpoints" => _dispatcher.Send(new GiftPointsTag { CommandEventArgs = eventArgs, Args = args }),
                "checkpoints" => _dispatcher.Send(new CheckPointsTag { CommandEventArgs = eventArgs, Args = args }),

                _ => Task.FromResult(new CustomCommandResult()),
            };
        }

        private async Task ProcessTagsAndSayMessage(CommandEventArgs eventArgs, string commandText, bool respondAsStreamer, bool sourceOnly)
        {
            var messages = commandText.Split("\n");
            foreach (var oldMessage in messages)
            {
                var message = oldMessage;
                if (string.IsNullOrWhiteSpace(message)) continue;

                var result = await ProcessTags(eventArgs, message);
                if (result.Cancel) throw new SkipCooldownException();

                if (!string.IsNullOrWhiteSpace(result.Message))
                {
                    message = UnescapeTagsInMessages(result.Message);
                    if (respondAsStreamer)
                    {
                        await _twitchService.SendMessage(message);
                    }
                    else if (result.ReplyToMessage)
                    {
                        await ServiceBackbone.ResponseWithMessage(eventArgs, message, sourceOnly);
                    }
                    else
                    {
                        await ServiceBackbone.SendChatMessage(message, sourceOnly);
                    }
                }
            }
        }


        public string UnescapeTagsInMessages(string args)
        {
            var matches = UnEscapedRegex().Matches(args);
            foreach (Match match in matches.Cast<Match>())
            {
                args = args.Replace(match.Value, match.Groups[1].Value);
            }
            return args;
        }

        private string ReplaceFirstOccurrence(string Source, string Find, string Replace)
        {
            int Place = Source.IndexOf(Find);
            string result = Source.Remove(Place, Find.Length).Insert(Place, Replace);
            return result;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Started {moduledname}", ModuleName);
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopped {moduledname}", ModuleName);
            return Task.CompletedTask;
        }
    }
}