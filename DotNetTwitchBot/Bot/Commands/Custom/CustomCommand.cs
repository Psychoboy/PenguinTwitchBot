using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using DotNetTwitchBot.Bot.Commands.Custom.Tags.PlayerSound;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Commands.Misc;
using DotNetTwitchBot.Bot.Commands.TTS;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.KickServices;
using DotNetTwitchBot.Bot.Models.Commands;
using DotNetTwitchBot.Bot.TwitchServices;
using DotNetTwitchBot.Repository;
using MediatR;
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
        readonly Dictionary<string, CustomCommands> Commands = [];
        static readonly SemaphoreSlim _semaphoreSlim = new(1);
        List<KeywordWithRegex> Keywords = [];
        private readonly IMediator _mediator;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CustomCommand> _logger;
        private readonly ITwitchService _twitchService;
        private readonly IKickService _kickService;
        private readonly IPointsSystem _PointsSystem;

        public CustomCommand(
            IMediator mediator,
            IViewerFeature viewerFeature,
            IServiceScopeFactory scopeFactory,
            ILogger<CustomCommand> logger,
            ITwitchService twitchService,
            IKickService kickService,
            IPointsSystem pointsSystem,
            IServiceBackbone serviceBackbone,
            ICommandHandler commandHandler) : base(serviceBackbone, commandHandler, "CustomCommands", mediator)
        {
            _mediator = mediator;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _twitchService = twitchService;
            _PointsSystem = pointsSystem;
            _kickService = kickService;

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

        public Dictionary<string, CustomCommands> GetCustomCommands()
        {
            return Commands;
        }

        public List<KeywordType> GetKeywords()
        {
            return Keywords.Select(x => x.Keyword).ToList();
        }

        public async Task<CustomCommands?> GetCustomCommand(int id)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.CustomCommands.Find(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task<KeywordType?> GetKeyword(int id)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.Keywords.Find(x => x.Id == id).FirstOrDefaultAsync();
        }



        private async Task LoadCommands()
        {
            _logger.LogInformation("Loading custom commands");
            var count = 0;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                Commands.Clear();
                var commands = await db.CustomCommands.GetAsync(includeProperties: "PointType");
                foreach (var command in commands)
                {
                    Commands[command.CommandName] = command;
                    count++;
                }

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
            _logger.LogInformation("Finished loading commands: {count}", count);
        }

        public async Task AddCommand(CustomCommands customCommand)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                customCommand.CommandName = customCommand.CommandName.ToLower();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                if ((await db.CustomCommands.Find(x => x.CommandName.Equals(customCommand.CommandName)).FirstOrDefaultAsync()) != null)
                {
                    _logger.LogWarning("Command already exists");
                    return;
                }
                await db.CustomCommands.AddAsync(customCommand);
                Commands[customCommand.CommandName] = customCommand;
                await db.SaveChangesAsync();
            }
            await LoadCommands();
        }

        public async Task DeleteCommand(CustomCommands customCommand)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                db.CustomCommands.Remove(customCommand);
                await db.SaveChangesAsync();
            }
            await LoadCommands();
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

        public async Task SaveCommand(CustomCommands customCommand)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                db.CustomCommands.Update(customCommand);
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
            foreach (var keyword in Keywords)
            {
                if (await CommandHandler.IsCoolDownExpired(e.Name, e.Platform, "keyword " + keyword.Keyword.CommandName) == false) continue;
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

                    await ProcessTagsAndSayMessage(commandEventArgs, keyword.Keyword.Response, false);

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

        public async Task RunCommand(CommandEventArgs e)
        {
            if (Commands.ContainsKey(e.Command) == false) return;

            if (Commands[e.Command].Disabled)
            {
                return;
            }

            if(Bot.Commands.CommandHandler.CheckToRunBroadcasterOnly(e, Commands[e.Command]) == false) return;

            if ((await CommandHandler.CheckPermission(Commands[e.Command], e)) == false)
            {
                return;
            }

            if (Commands[e.Command].Platforms.Contains(e.Platform) == false)
            {
                return;
            }

            await ExecuteCommand(e);
        }


        private async Task ExecuteCommand(CommandEventArgs e)
        {
            try
            {
                if (false == e.SkipLock)
                {
                    if (await _semaphoreSlim.WaitAsync(500) == false)
                    {
                        _logger.LogWarning("CustomCommand Lock expired while waiting...");
                    }
                }

                var command = Commands[e.Command];
                if (command.SayCooldown)
                {
                    if (await CommandHandler.IsCoolDownExpiredWithMessage(e.Name, e.Platform, e.DisplayName, e.Command) == false) return;
                }
                else
                {
                    if (await CommandHandler.IsCoolDownExpired(e.Name, e.Platform, e.Command) == false) return;
                }

                if (command.Cost > 0 && command.PointType != null)
                {
                    if ((await _PointsSystem.RemovePointsFromUserByUserId(e.UserId, e.Platform, command.PointTypeId ?? 0, command.Cost)) == false)
                    {
                        await RespondWithMessage(e, $"you don't have enough {command.PointType?.Name}, that command costs {command.Cost}.");
                        return;
                    }
                }

                await ProcessTagsAndSayMessage(e, command.Response, command.RespondAsStreamer);
                if (command.GlobalCooldown > 0)
                {
                    await CommandHandler.AddGlobalCooldown(e.Command, command.GlobalCooldown);
                }
                if (command.UserCooldown > 0)
                {
                    await CommandHandler.AddCoolDown(e.Name, e.Command, command.UserCooldown);
                }
            }
            finally
            {
                if (false == e.SkipLock)
                {
                    _semaphoreSlim.Release();
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

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var defaultCommand = CommandHandler.GetCommand(e.Command);
            if (defaultCommand == null) return;
            switch (defaultCommand.CommandProperties.CommandName)
            {
                case "addcommand":
                    {
                        try
                        {
                            var newCommand = JsonSerializer.Deserialize<CustomCommands>(e.Arg);
                            if (newCommand != null)
                            {
                                await AddCommand(newCommand);
                                await ServiceBackbone.SendChatMessage("Successfully added command", e.Platform);
                            }
                            else
                            {
                                await ServiceBackbone.SendChatMessage("failed to add command", e.Platform);
                            }

                        }
                        catch (Exception err)
                        {
                            _logger.LogError(err, "Failed to add command");
                        }

                        return;
                    }

                case "refreshcommands":
                    {
                        await LoadCommands();
                        return;
                    }

                case "disablecommand":
                    {
                        if (!Commands.TryGetValue(e.Arg, out CustomCommands? value)) return;
                        await using (var scope = _scopeFactory.CreateAsyncScope())
                        {
                            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                            var command = await db.CustomCommands.Find(x => x.CommandName.Equals(e.Arg)).FirstOrDefaultAsync();
                            if (command == null)
                            {
                                await ServiceBackbone.SendChatMessage(string.Format("Failed to disable {0}", e.Arg), e.Platform);
                                return;
                            }
                            command.Disabled = true;
                            value.Disabled = true;
                            db.CustomCommands.Update(command);
                            await db.SaveChangesAsync();
                        }
                        await ServiceBackbone.SendChatMessage(string.Format("Disabled {0}", e.Arg), e.Platform);
                        return;
                    }

                case "enablecommand":
                    {
                        if (!Commands.TryGetValue(e.Arg, out CustomCommands? value)) return;
                        await using (var scope = _scopeFactory.CreateAsyncScope())
                        {
                            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                            var command = await db.CustomCommands.Find(x => x.CommandName.Equals(e.Arg)).FirstOrDefaultAsync();
                            if (command == null)
                            {
                                await ServiceBackbone.SendChatMessage(string.Format("Failed to enable {0}", e.Arg), e.Platform);
                                return;
                            }
                            command.Disabled = false;
                            value.Disabled = false;
                            db.CustomCommands.Update(command);
                            await db.SaveChangesAsync();
                        }
                        await ServiceBackbone.SendChatMessage(string.Format("Enabled {0}", e.Arg), e.Platform);
                        return;
                    }

            }

        }

        public string CustomCommandResponse(string command)
        {
            return Commands[command].Response;
        }

        public bool CustomCommandExists(string command)
        {
            return Commands.ContainsKey(command);
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
                "alert" => _mediator.Send(new AlertTag { CommandEventArgs = eventArgs, Args = args }),
                "playsound" => _mediator.Send(new PlaySoundTag { CommandEventArgs = eventArgs, Args = args }),
                "sender" => _mediator.Send(new SenderTag { CommandEventArgs = eventArgs, Args = args }),
                "args" => _mediator.Send(new ArgsTag { CommandEventArgs = eventArgs, Args = args }),
                "randomint" => _mediator.Send(new RandomIntTag { CommandEventArgs = eventArgs, Args = args }),
                "useronly" => _mediator.Send(new UserOnlyTag { CommandEventArgs = eventArgs, Args = args }),
                "writefile" => _mediator.Send(new WriteFileTag { CommandEventArgs = eventArgs, Args = args }),
                "currenttime" => _mediator.Send(new CurrentTimeTag { CommandEventArgs = eventArgs, Args = args }),
                "@sender" => _mediator.Send(new AtSenderTag { CommandEventArgs = eventArgs, Args = args }),
                "onlineonly" => _mediator.Send(new OnlineOnlyTag { CommandEventArgs = eventArgs, Args = args }),
                "offlineonly" => _mediator.Send(new OfflineOnlyTag { CommandEventArgs = eventArgs, Args = args }),
                "followage" => _mediator.Send(new FollowAgeTag { CommandEventArgs = eventArgs, Args = args }),
                "multicounter" => _mediator.Send(new MultiCounterTag { CommandEventArgs = eventArgs, Args = args }),
                "multicounteralert" => _mediator.Send(new MultiCounterAlertTag { CommandEventArgs = eventArgs, Args = args }),
                "uptime" => _mediator.Send(new UptimeTag { CommandEventArgs = eventArgs, Args = args }),
                "customapitext" => _mediator.Send(new CustomApiTextTag { CommandEventArgs = eventArgs, Args = args }),
                "customapinoresponse" => _mediator.Send(new CustomApiNoResponseTag { CommandEventArgs = eventArgs, Args = args }),
                "giveawayprize" => _mediator.Send(new GiveawayPrizeTag { CommandEventArgs = eventArgs, Args = args }),
                "target" => _mediator.Send(new TargetTag { CommandEventArgs = eventArgs, Args = args }),
                "targetorself" => _mediator.Send(new TargetOrSelfTag { CommandEventArgs = eventArgs, Args = args }),
                "watchtime" => _mediator.Send(new WatchTimeTag { CommandEventArgs = eventArgs, Args = args }),
                "command" => _mediator.Send(new ExecuteCommandTag { CommandEventArgs = eventArgs, Args = args }),
                "elevatedcommand" => _mediator.Send(new ExecuteElevatedCommandTag { CommandEventArgs = eventArgs, Args = args }),
                "ttsandprint" => _mediator.Send(new TTSAndPrintTag { CommandEventArgs = eventArgs, Args = args }),
                "tts" => _mediator.Send(new TTSTag { CommandEventArgs = eventArgs, Args = args }),
                "enablechannelpoint" => _mediator.Send(new EnableChannelPointTag { CommandEventArgs = eventArgs, Args = args }),
                "disablechannelpoint" => _mediator.Send(new DisableChannelPointTag { CommandEventArgs = eventArgs, Args = args }),
                "pausechannelpoint" => _mediator.Send(new PauseChannelPointTag { CommandEventArgs = eventArgs, Args = args }),
                "unpausechannelpoint" => _mediator.Send(new UnpauseChannelPointTag { CommandEventArgs = eventArgs, Args = args }),
                "enabletimergroup" => _mediator.Send(new EnableTimerGroupTag { CommandEventArgs = eventArgs, Args = args }),
                "disabletimergroup" => _mediator.Send(new DisableTimerGroupTag { CommandEventArgs = eventArgs, Args = args }),
                "enablecommand" => _mediator.Send(new EnableCommandTag { CommandEventArgs = eventArgs, Args = args, CustomCommand = this }),
                "disablecommand" => _mediator.Send(new DisableCommandTag { CommandEventArgs = eventArgs, Args = args, CustomCommand = this }),
                "giftpoints" => _mediator.Send(new GiftPointsTag { CommandEventArgs = eventArgs, Args = args }),
                "checkpoints" => _mediator.Send(new CheckPointsTag { CommandEventArgs = eventArgs, Args = args }),

                _ => Task.FromResult(new CustomCommandResult()),
            };
        }

        private async Task ProcessTagsAndSayMessage(CommandEventArgs eventArgs, string commandText, bool respondAsStreamer)
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
                        if (eventArgs.Platform == PlatformType.Kick)
                        {
                            await _kickService.SendMessageAsStreamer(message);
                        }
                        else
                        {
                            await _twitchService.SendMessage(message);
                        }
                    }
                    else if (result.ReplyToMessage )
                    {
                        await ServiceBackbone.ResponseWithMessage(eventArgs, message);
                    }
                    else
                    {
                        await ServiceBackbone.SendChatMessage(message, eventArgs.Platform);
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