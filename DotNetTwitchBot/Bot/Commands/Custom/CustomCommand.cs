using DotNetTwitchBot.Application.Alert.Notification;
using DotNetTwitchBot.Bot.Alerts;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Commands.Misc;
using DotNetTwitchBot.Bot.Commands.TTS;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.TwitchServices;
using DotNetTwitchBot.Repository;
using MediatR;
using System.Security.Cryptography;
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
        private static partial Regex CounterRegex();

        readonly Dictionary<string, Func<CommandEventArgs, string, Task<CustomCommandResult>>> CommandTags = [];
        readonly Dictionary<string, Models.CustomCommands> Commands = [];
        static readonly SemaphoreSlim _semaphoreSlim = new(1);
        List<Models.KeywordWithRegex> Keywords = [];
        private readonly IMediator _mediator;
        private readonly IViewerFeature _viewerFeature;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CustomCommand> _logger;
        private readonly ITwitchService _twitchService;
        private readonly ILoyaltyFeature _loyaltyFeature;
        private readonly GiveawayFeature _giveawayFeature;
        private readonly ITTSService _ttsService;
        private readonly AutoTimers _timers;

        public CustomCommand(
            IMediator mediator,
            IViewerFeature viewerFeature,
            IServiceScopeFactory scopeFactory,
            ILogger<CustomCommand> logger,
            ITwitchService twitchService,
            ILoyaltyFeature loyaltyFeature,
            GiveawayFeature giveawayFeature,
            IServiceBackbone serviceBackbone,
            ITTSService ttsService,
            AutoTimers timers,
            ICommandHandler commandHandler) : base(serviceBackbone, commandHandler, "CustomCommands")
        {
            _mediator = mediator;
            _viewerFeature = viewerFeature;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _twitchService = twitchService;
            _loyaltyFeature = loyaltyFeature;
            _giveawayFeature = giveawayFeature;
            _ttsService = ttsService;
            _timers = timers;

            //RegisterCommands Here
            CommandTags.Add("alert", Alert);
            CommandTags.Add("playsound", PlaySound);
            CommandTags.Add("sender", Sender);
            CommandTags.Add("args", Args);
            CommandTags.Add("randomint", RandomInt);

            CommandTags.Add("useronly", UserOnly);
            CommandTags.Add("writefile", WriteFile);
            CommandTags.Add("currenttime", CurrentTime);
            CommandTags.Add("@sender", AtSender);
            CommandTags.Add("customapitext", CustomApiText);
            CommandTags.Add("onlineonly", OnlineOnly);
            CommandTags.Add("offlineonly", OfflineOnly);
            CommandTags.Add("followage", FollowAge);
            CommandTags.Add("multicounter", MultiCounter);
            CommandTags.Add("price", Price);
            CommandTags.Add("pointname", PointName);
            CommandTags.Add("channelname", ChannelName);
            CommandTags.Add("uptime", Uptime);
            CommandTags.Add("customapinoresponse", CustomApiNoResponse);
            CommandTags.Add("GiveawayPrize", GiveawayPrize);
            CommandTags.Add("target", Target);
            CommandTags.Add("targetorself", TargetOrSelf);
            CommandTags.Add("watchtime", WatchTime);
            CommandTags.Add("command", ExecuteCommand);
            CommandTags.Add("elevatedcommand", ExecuteElevatedCommand);
            CommandTags.Add("ttsandprint", TTSAndPrint);
            CommandTags.Add("tts", TTS);
            CommandTags.Add("enablechannelpoint", EnableChannelPoint);
            CommandTags.Add("disablechannelpoint", DisableChannelPoint);
            CommandTags.Add("pausechannelpoint", PauseChannelPoint);
            CommandTags.Add("unpausechannelpoint", UnpauseChannelPoint);
            CommandTags.Add("enabletimergroup", EnableTimerGroup);
            CommandTags.Add("disabletimergroup", DisableTimerGroup);
            CommandTags.Add("enablecommand", EnableCommand);
            CommandTags.Add("disablecommand", DisableCommand);
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
                var commands = await db.CustomCommands.GetAllAsync();
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
                    keyword.Regex = new Regex(keyword.Keyword.CommandName);
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
            bool match = false;
            foreach (var keyword in Keywords)
            {
                if (CommandHandler.IsCoolDownExpired(e.Name, keyword.Keyword.CommandName) == false) continue;
                if (keyword.Keyword.IsRegex)
                {
                    if (keyword.Regex.IsMatch(e.Message)) match = true;
                }
                else
                {
                    if (keyword.Keyword.IsCaseSensitive)
                    {
                        if (e.Message.Contains(keyword.Keyword.CommandName, StringComparison.CurrentCulture)) match = true;
                    }
                    else
                    {
                        if (e.Message.Contains(keyword.Keyword.CommandName, StringComparison.CurrentCultureIgnoreCase)) match = true;
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
                    await ProcessTagsAndSayMessage(commandEventArgs, keyword.Keyword.Response, false);
                    if (Commands[keyword.Keyword.CommandName].GlobalCooldown > 0)
                    {
                        CommandHandler.AddGlobalCooldown(keyword.Keyword.CommandName, Commands[keyword.Keyword.CommandName].GlobalCooldown);
                    }
                    if (Commands[keyword.Keyword.CommandName].UserCooldown > 0)
                    {
                        CommandHandler.AddCoolDown(e.Name, keyword.Keyword.CommandName, Commands[keyword.Keyword.CommandName].UserCooldown);
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

            if ((await CommandHandler.CheckPermission(Commands[e.Command], e)) == false)
            {
                return;
            }
            await ExecuteCommand(e);
        }

        private async Task ExecuteCommand(CommandEventArgs e)
        {
            try
            {
                if (await _semaphoreSlim.WaitAsync(500) == false)
                {
                    _logger.LogWarning("CustomCommand Lock expired while waiting...");
                }

                var isCoolDownExpired = await CommandHandler.IsCoolDownExpiredWithMessage(e.Name, e.DisplayName, e.Command);
                if (isCoolDownExpired == false) return;
                if (Commands[e.Command].Cost > 0)
                {
                    if ((await _loyaltyFeature.RemovePointsFromUser(e.Name, Commands[e.Command].Cost)) == false)
                    {
                        await ServiceBackbone.SendChatMessage(e.DisplayName, $"you don't have enough pasties, that command costs {Commands[e.Command].Cost}.");
                        return;
                    }
                }

                if (Commands[e.Command].GlobalCooldown > 0)
                {
                    CommandHandler.AddGlobalCooldown(e.Command, Commands[e.Command].GlobalCooldown);
                }
                if (Commands[e.Command].UserCooldown > 0)
                {
                    CommandHandler.AddCoolDown(e.Name, e.Command, Commands[e.Command].UserCooldown);
                }
            }
            finally
            {
                _semaphoreSlim.Release();
            }

            await ProcessTagsAndSayMessage(e, Commands[e.Command].Response, Commands[e.Command].RespondAsStreamer);
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
                                await ServiceBackbone.SendChatMessage("Successfully added command");
                            }
                            else
                            {
                                await ServiceBackbone.SendChatMessage("failed to add command");
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
                                await ServiceBackbone.SendChatMessage(string.Format("Failed to disable {0}", e.Arg));
                                return;
                            }
                            command.Disabled = true;
                            value.Disabled = true;
                            db.CustomCommands.Update(command);
                            await db.SaveChangesAsync();
                        }
                        await ServiceBackbone.SendChatMessage(string.Format("Disabled {0}", e.Arg));
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
                                await ServiceBackbone.SendChatMessage(string.Format("Failed to enable {0}", e.Arg));
                                return;
                            }
                            command.Disabled = false;
                            value.Disabled = false;
                            db.CustomCommands.Update(command);
                            await db.SaveChangesAsync();
                        }
                        await ServiceBackbone.SendChatMessage(string.Format("Enabled {0}", e.Arg));
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

                        if (CommandTags.ContainsKey(tagName.Value.Trim()))
                        {
                            thisTagFound = true;
                            CustomCommandResult result = await CommandTags[tagName.Value.Trim()](eventArgs, tagArgs.Value.Trim());
                            if (result.Cancel)
                            {
                                cancel = true;
                                break;
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
                return new CustomCommandResult(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running custom command.");
                return new CustomCommandResult(true);
            }
        }

        private async Task ProcessTagsAndSayMessage(CommandEventArgs eventArgs, string commandText, bool respondAsStreamer)
        {
            var messages = commandText.Split("\n");
            foreach (var oldMessage in messages)
            {
                var message = oldMessage;
                if (string.IsNullOrWhiteSpace(message)) continue;

                var result = await ProcessTags(eventArgs, message);
                if (result.Cancel) return;

                if (!string.IsNullOrWhiteSpace(result.Message))
                {
                    message = UnescapeTagsInMessages(result.Message);
                    if (respondAsStreamer)
                    {
                        await _twitchService.SendMessage(message);
                    }
                    else
                    {
                        await ServiceBackbone.SendChatMessage(message);
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

        private async Task<CustomCommandResult> Alert(CommandEventArgs eventArgs, string args)
        {
            return await Task.Run(() =>
            {
                var alertImage = new AlertImage();
                _mediator.Publish(new QueueAlert(alertImage.Generate(args)));
                return new CustomCommandResult();
            });
        }

        private async Task<CustomCommandResult> Sender(CommandEventArgs eventArgs, string args)
        {
            return await Task.Run(() =>
            {
                return new CustomCommandResult(eventArgs.DisplayName);
            });
        }

        private async Task<CustomCommandResult> AtSender(CommandEventArgs eventArgs, string args)
        {
            return await Task.Run(() =>
            {
                if (eventArgs.IsDiscord)
                {
                    return new CustomCommandResult(eventArgs.DiscordMention);
                }
                else
                {
                    return new CustomCommandResult(string.Format("@{0}, ", eventArgs.DisplayName));
                }
            });
        }

        private async Task<CustomCommandResult> PlaySound(CommandEventArgs eventArgs, string args)
        {
            return await Task.Run(() =>
            {
                var alertSound = new AlertSound
                {
                    AudioHook = args
                };
                _mediator.Publish(new QueueAlert(alertSound.Generate()));
                return new CustomCommandResult();
            });
        }

        private async Task<CustomCommandResult> UserOnly(CommandEventArgs eventArgs, string args)
        {
            return await Task.Run(() =>
            {
                if (eventArgs.Name.Equals(args, StringComparison.CurrentCultureIgnoreCase)) return new CustomCommandResult();
                return new CustomCommandResult(true);
            });

        }

        private async Task<CustomCommandResult> WriteFile(CommandEventArgs eventArgs, string args)
        {
            var parseResults = args.Split(",");
            if (parseResults.Length < 3) return new CustomCommandResult(args);
            var fileName = parseResults[0];
            var append = Boolean.Parse(parseResults[1]);
            var text = parseResults[2];
            if (!append)
            {
                await File.WriteAllTextAsync(fileName, text);
            }
            else
            {
                await File.AppendAllTextAsync(fileName, "\n" + text);
            }
            return new CustomCommandResult();
        }

        private async Task<CustomCommandResult> CurrentTime(CommandEventArgs eventArgs, string args)
        {
            return await Task.Run(() =>
            {
                return new CustomCommandResult(DateTime.Now.ToString("h:mm:ss tt"));
            });
        }

        private async Task<CustomCommandResult> CustomApiText(CommandEventArgs eventArgs, string args)
        {
            var httpClient = new HttpClient();
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri(args),
                Method = HttpMethod.Get
            };
            request.Headers.Add("Accept", "text/plain");
            var result = await httpClient.SendAsync(request);
            return new CustomCommandResult(await result.Content.ReadAsStringAsync());
        }

        private async Task<CustomCommandResult> EnableChannelPoint(CommandEventArgs eventArgs, string args)
        {
            _logger.LogInformation("Trying to enable {title} channel point.", args);
            var channelPoints = await _twitchService.GetChannelPointRewards(true);
            foreach (var channelPoint in channelPoints)
            {
                if (channelPoint.Title.Equals(args.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward.UpdateCustomRewardRequest request = new()
                    {
                        IsEnabled = true
                    };
                    _logger.LogInformation("Channel point {title} enabled.", channelPoint.Title);
                    await _twitchService.UpdateChannelPointReward(channelPoint.Id, request);
                    break;
                }
            }
            return new CustomCommandResult();
        }

        private async Task<CustomCommandResult> DisableChannelPoint(CommandEventArgs eventArgs, string args)
        {
            var channelPoints = await _twitchService.GetChannelPointRewards(true);
            foreach (var channelPoint in channelPoints)
            {
                if (channelPoint.Title.Equals(args, StringComparison.OrdinalIgnoreCase))
                {
                    TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward.UpdateCustomRewardRequest request = new()
                    {
                        IsEnabled = false
                    };
                    await _twitchService.UpdateChannelPointReward(channelPoint.Id, request);
                    break;
                }
            }
            return new CustomCommandResult();
        }

        private async Task<CustomCommandResult> EnableTimerGroup(CommandEventArgs eventArgs, string args)
        {
            _logger.LogInformation("Trying to enable {name} title", args);
            var timers = await _timers.GetTimerGroupsAsync();
            var timerGroup = timers.Where(x => x.Name.Equals(args.Trim(), StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (timerGroup != null)
            {
                timerGroup = await _timers.UpdateNextRun(timerGroup);
                timerGroup.Active = true;
                await _timers.UpdateTimerGroup(timerGroup);
                _logger.LogInformation("Enabled {name} timer.", args);
            }
            return new CustomCommandResult();
        }

        private async Task<CustomCommandResult> DisableTimerGroup(CommandEventArgs eventArgs, string args)
        {
            var timers = await _timers.GetTimerGroupsAsync();
            var timerGroup = timers.Where(x => x.Name.Equals(args.Trim(), StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (timerGroup != null)
            {
                timerGroup.Active = false;
                await _timers.UpdateTimerGroup(timerGroup);
            }
            return new CustomCommandResult();
        }

        private async Task<CustomCommandResult> EnableCommand(CommandEventArgs eventArgs, string args)
        {
            var commands = GetCustomCommands();

            if (commands.TryGetValue(args.Trim(), out var command))
            {
                command.Disabled = false;
                await SaveCommand(command);
            }
            return new CustomCommandResult();
        }

        private async Task<CustomCommandResult> DisableCommand(CommandEventArgs eventArgs, string args)
        {
            var commands = GetCustomCommands();

            if (commands.TryGetValue(args.Trim(), out var command))
            {
                command.Disabled = true;
                await SaveCommand(command);
            }
            return new CustomCommandResult();
        }

        private async Task<CustomCommandResult> PauseChannelPoint(CommandEventArgs eventArgs, string args)
        {
            var channelPoints = await _twitchService.GetChannelPointRewards(true);
            foreach (var channelPoint in channelPoints)
            {
                if (channelPoint.Title.Equals(args, StringComparison.OrdinalIgnoreCase))
                {
                    TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward.UpdateCustomRewardRequest request = new()
                    {
                        IsPaused = true
                    };
                    await _twitchService.UpdateChannelPointReward(channelPoint.Id, request);
                    break;
                }
            }
            return new CustomCommandResult();
        }

        private async Task<CustomCommandResult> UnpauseChannelPoint(CommandEventArgs eventArgs, string args)
        {
            var channelPoints = await _twitchService.GetChannelPointRewards(true);
            foreach (var channelPoint in channelPoints)
            {
                if (channelPoint.Title.Equals(args, StringComparison.OrdinalIgnoreCase))
                {
                    TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward.UpdateCustomRewardRequest request = new()
                    {
                        IsPaused = false
                    };
                    await _twitchService.UpdateChannelPointReward(channelPoint.Id, request);
                    break;
                }
            }
            return new CustomCommandResult();
        }

        private async Task<CustomCommandResult> CustomApiNoResponse(CommandEventArgs eventArgs, string args)
        {
            var httpClient = new HttpClient();
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri(args),
                Method = HttpMethod.Get
            };
            request.Headers.Add("Accept", "text/plain");
            _ = await httpClient.SendAsync(request);
            return new CustomCommandResult();
        }

        private async Task<CustomCommandResult> TTS(CommandEventArgs args, string arg2)
        {
            var voice = await _ttsService.GetRandomVoice(args.Name);
            _ttsService.SayMessage(voice, arg2);
            return new CustomCommandResult();
        }

        private Task<CustomCommandResult> Args(CommandEventArgs args, string arg2)
        {
            return Task.FromResult(new CustomCommandResult(args.Arg));
        }

        private async Task<CustomCommandResult> TTSAndPrint(CommandEventArgs args, string arg2)
        {
            var voice = await _ttsService.GetRandomVoice(args.Name);
            _ttsService.SayMessage(voice, arg2);
            return new CustomCommandResult(arg2);
        }

        private async Task<CustomCommandResult> OnlineOnly(CommandEventArgs eventArgs, string args)
        {
            return await Task.Run(() =>
            {
                return ServiceBackbone.IsOnline ? new CustomCommandResult() : new CustomCommandResult(true);
            });
        }

        private async Task<CustomCommandResult> OfflineOnly(CommandEventArgs eventArgs, string args)
        {
            return await Task.Run(() =>
            {
                return ServiceBackbone.IsOnline ? new CustomCommandResult(true) : new CustomCommandResult();
            });
        }

        private async Task<CustomCommandResult> RandomInt(CommandEventArgs eventArgs, string args)
        {
            if (string.IsNullOrEmpty(args)) return new CustomCommandResult();
            var vals = args.Split(',');
            if (vals.Length < 2) return new CustomCommandResult();
            var val1 = int.Parse(vals[0]);
            var val2 = int.Parse(vals[1]);
            return await Task.Run(() => new CustomCommandResult(RandomNumberGenerator.GetInt32(val1, val2).ToString()));
        }

        private async Task<CustomCommandResult> FollowAge(CommandEventArgs eventArgs, string args)
        {
            if (string.IsNullOrWhiteSpace(args)) args = eventArgs.Name;
            var follower = await _viewerFeature.GetFollowerAsync(args);
            if (follower == null)
            {
                return new CustomCommandResult(string.Format("{0} is not a follower", args));
            }
            return new CustomCommandResult(string.Format("{0} has been following since {1} ({2} days ago).",
            follower.DisplayName, follower.FollowDate.ToLongDateString(), Convert.ToInt32((DateTime.Now - follower.FollowDate).TotalDays)));
        }

        private async Task<CustomCommandResult> MultiCounter(CommandEventArgs eventArgs, string args)
        {
            var match = CounterRegex().Match(args);
            var counterName = "";
            var counterAlert = "";

            if (match.Groups.Count > 0)
            {
                counterName = match.Groups[1].Value;
                counterAlert = match.Groups[2].Value;
            }
            else
            {
                counterName = args;
            }
            var amount = 0;
            //Fix counter here for alerts!
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var counter = await db.Counters.Find(x => x.CounterName.Equals(counterName)).FirstOrDefaultAsync();
                if (counter == null)
                {
                    counter = new Counter()
                    {
                        CounterName = counterName,
                        Amount = 0
                    };
                    await db.Counters.AddAsync(counter);
                }

                // TODO: Make this customizable
                if (eventArgs.Args.Count > 0 && (eventArgs.IsBroadcaster || eventArgs.IsMod))
                {
                    var modifier = eventArgs.Args[0];

                    if (modifier.Equals("reset"))
                    {
                        counter.Amount = 0;
                    }
                    else if (modifier.Equals("+"))
                    {
                        counter.Amount++;
                    }
                    else if (modifier.Equals("-"))
                    {
                        counter.Amount--;
                    }
                    else if (modifier.Equals("set"))
                    {
                        if (eventArgs.Args.Count >= 2)
                        {
                            if (Int32.TryParse(eventArgs.Args[1], out var newAmount))
                            {
                                counter.Amount = newAmount;
                            }
                        }
                    }
                    await db.SaveChangesAsync();
                    await WriteCounterFile(counterName, counter.Amount);
                }
                amount = counter.Amount;
            }
            counterAlert = counterAlert.Replace("\\(totalcount\\)", amount.ToString());
            if (!string.IsNullOrWhiteSpace(counterAlert))
            {
                var alertImage = new AlertImage();
                await _mediator.Publish(new QueueAlert(alertImage.Generate(counterAlert)));
            }

            return new CustomCommandResult(amount.ToString());
        }

        private async Task WriteCounterFile(string counterName, int amount)
        {
            if (!Directory.Exists("Data/counters"))
            {
                Directory.CreateDirectory("Data/counters");
            }
            await File.WriteAllTextAsync($"Data/counters/{counterName}.txt", amount.ToString());
            await File.WriteAllTextAsync($"Data/counters/{counterName}-full.txt", counterName + ": " + amount.ToString());
        }

        private async Task<CustomCommandResult> Price(CommandEventArgs eventArgs, string args)
        {
            return await Task.Run(() =>
           {
               return new CustomCommandResult();
           });
        }

        private async Task<CustomCommandResult> PointName(CommandEventArgs eventArgs, string args)
        {
            return await Task.Run(() =>
            {
                return new CustomCommandResult("Pasties");
            });
        }

        private async Task<CustomCommandResult> ChannelName(CommandEventArgs eventArgs, string args)
        {
            return await Task.Run(() =>
            {
                return new CustomCommandResult("SuperPenguinTV");
            });
        }

        private async Task<CustomCommandResult> GiveawayPrize(CommandEventArgs eventArgs, string args)
        {
            var prize = await _giveawayFeature.GetPrize();
            return new CustomCommandResult(prize);
        }

        private async Task<CustomCommandResult> Uptime(CommandEventArgs eventArgs, string args)
        {
            var streamTime = await _twitchService.StreamStartedAt();
            if (streamTime == DateTime.MinValue) return new CustomCommandResult("Stream is offline");
            var currentTime = DateTime.UtcNow;
            var totalTime = currentTime - streamTime;
            return new CustomCommandResult(totalTime.ToString(@"hh\:mm\:ss"));
        }

        private async Task<CustomCommandResult> Target(CommandEventArgs eventArgs, string args)
        {
            return await Task.Run(() =>
            {
                return new CustomCommandResult(eventArgs.TargetUser);
            });

        }

        private async Task<CustomCommandResult> TargetOrSelf(CommandEventArgs eventArgs, string args)
        {
            return await Task.Run(() =>
            {
                return new CustomCommandResult(string.IsNullOrWhiteSpace(eventArgs.TargetUser) ? eventArgs.Name : eventArgs.TargetUser);
            });
        }

        private async Task<CustomCommandResult> WatchTime(CommandEventArgs eventArgs, string args)
        {
            var time = await _loyaltyFeature.GetViewerWatchTime(args);
            return new CustomCommandResult(time);
        }

        private async Task<CustomCommandResult> ExecuteCommand(CommandEventArgs eventArgs, string args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                _logger.LogWarning("Missing args for custom command of 'command' type.");
                return new CustomCommandResult();
            }
            var commandArgs = args.Split(' ');
            var commandName = commandArgs[0];
            var newCommandArgs = new List<string>();
            var targetUser = "";
            if (commandArgs.Length > 1)
            {
                newCommandArgs.AddRange(commandArgs.Skip(1));
                targetUser = commandArgs[1];
            }
            var command = new CommandEventArgs
            {
                Command = commandName,
                Arg = string.Join(" ", newCommandArgs),
                Args = newCommandArgs,
                TargetUser = targetUser,
                IsWhisper = eventArgs.IsWhisper,
                IsDiscord = eventArgs.IsDiscord,
                DiscordMention = eventArgs.DiscordMention,
                FromAlias = eventArgs.FromAlias,
                IsSub = eventArgs.IsSub,
                IsMod = eventArgs.IsMod,
                IsVip = eventArgs.IsVip,
                IsBroadcaster = eventArgs.IsBroadcaster,
                DisplayName = eventArgs.DisplayName,
                Name = eventArgs.Name,
                SkipLock = true
            };
            await ServiceBackbone.RunCommand(command);
            return new CustomCommandResult();
        }

        private async Task<CustomCommandResult> ExecuteElevatedCommand(CommandEventArgs eventArgs, string args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                _logger.LogWarning("Missing args for custom command of 'elevatedcommand' type.");
                return new CustomCommandResult();
            }

            var commandArgs = args.Split(' ');
            if (commandArgs.Length < 2)
            {
                _logger.LogWarning("Missing required args for custom command of 'elevatedcommand' type.");
                return new CustomCommandResult();
            }
            var commandName = commandArgs[0];
            var commandPermission = commandArgs[1];
            var newCommandArgs = new List<string>();

            var targetUser = "";
            if (commandArgs.Length > 2)
            {
                newCommandArgs.AddRange(commandArgs.Skip(2));
                targetUser = commandArgs[2];
            }

            var command = new CommandEventArgs
            {
                Command = commandName,
                Arg = string.Join(" ", newCommandArgs),
                Args = newCommandArgs,
                TargetUser = targetUser,
                IsWhisper = eventArgs.IsWhisper,
                IsDiscord = eventArgs.IsDiscord,
                DiscordMention = eventArgs.DiscordMention,
                FromAlias = eventArgs.FromAlias,
                IsSub = commandPermission.Equals("sub") || eventArgs.IsSub,
                IsMod = commandPermission.Equals("mod") || eventArgs.IsMod,
                IsVip = commandPermission.Equals("vip") || eventArgs.IsVip,
                IsBroadcaster = commandPermission.Equals("broadcaster") || eventArgs.IsBroadcaster,
                DisplayName = eventArgs.DisplayName,
                Name = eventArgs.Name,
                SkipLock = true
            };

            await ServiceBackbone.RunCommand(command);
            return new CustomCommandResult();
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