using System.Runtime.CompilerServices;
using System.Net;
using System.ComponentModel.Design;
using System.Text.RegularExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Alerts;
using DotNetTwitchBot.Bot.Commands.Features;
using System.Text.Json;
using DotNetTwitchBot.Bot.TwitchServices;

namespace DotNetTwitchBot.Bot.Commands.Custom
{
    public class CustomCommand : BaseCommand
    {
        Dictionary<string, Func<CommandEventArgs, string, Task<CustomCommandResult>>> CommandTags = new Dictionary<string, Func<CommandEventArgs, string, Task<CustomCommandResult>>>();
        Dictionary<string, Models.CustomCommands> Commands = new Dictionary<string, Models.CustomCommands>();
        static SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1);
        List<Models.KeywordWithRegex> Keywords = new List<KeywordWithRegex>();
        private SendAlerts _sendAlerts;
        private ViewerFeature _viewerFeature;
        private readonly IServiceScopeFactory _scopeFactory;
        private ILogger<CustomCommand> _logger;
        private TwitchService _twitchService;
        private LoyaltyFeature _loyaltyFeature;
        private GiveawayFeature _giveawayFeature;

        public CustomCommand(
            SendAlerts sendAlerts,
            ViewerFeature viewerFeature,
            IServiceScopeFactory scopeFactory,
            ILogger<CustomCommand> logger,
            TwitchService twitchService,
            LoyaltyFeature loyaltyFeature,
            GiveawayFeature giveawayFeature,
            ServiceBackbone serviceBackbone) : base(serviceBackbone)
        {
            _sendAlerts = sendAlerts;
            _viewerFeature = viewerFeature;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _twitchService = twitchService;
            _loyaltyFeature = loyaltyFeature;
            _giveawayFeature = giveawayFeature;
            _serviceBackbone.ChatMessageEvent += OnChatMessage;

            //RegisterCommands Here
            CommandTags.Add("alert", Alert);
            CommandTags.Add("sender", Sender);
            CommandTags.Add("playsound", PlaySound);
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
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                return await db.CustomCommands.Where(x => x.Id == id).FirstOrDefaultAsync();
            }
        }

        public async Task<KeywordType?> GetKeyword(int id)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                return await db.Keywords.Where(x => x.Id == id).FirstOrDefaultAsync();
            }
        }

        public async Task LoadCommands()
        {
            _logger.LogInformation("Loading commands");
            var count = 0;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Commands.Clear();
                var commands = await db.CustomCommands.ToListAsync();
                foreach (var command in commands)
                {
                    Commands[command.CommandName] = command;
                    count++;
                }

                _logger.LogInformation("Loading keywords");
                Keywords.Clear();
                Keywords = (await db.Keywords.ToListAsync()).Select(x => new KeywordWithRegex(x)).ToList();
            }

            foreach (var keyword in Keywords)
            {
                if (keyword.Keyword.IsRegex)
                {
                    keyword.Regex = new Regex(keyword.Keyword.CommandName);
                }
            }
            _logger.LogInformation("Finished loading commands: {0}", count);
        }

        public async Task AddCommand(CustomCommands customCommand)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                if ((await db.CustomCommands.Where(x => x.CommandName.Equals(customCommand.CommandName)).FirstOrDefaultAsync()) != null)
                {
                    _logger.LogWarning("Command already exists");
                    return;
                }
                await db.CustomCommands.AddAsync(customCommand);
                Commands[customCommand.CommandName] = customCommand;
                await db.SaveChangesAsync();
            }
        }

        public async Task AddKeyword(KeywordType keyword)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                if ((await db.Keywords.Where(x => x.CommandName.Equals(keyword.CommandName)).FirstOrDefaultAsync()) != null)
                {
                    _logger.LogWarning("Keyword already exists");
                    return;
                }
                await db.Keywords.AddAsync(keyword);
                await db.SaveChangesAsync();
            }
            await LoadCommands();
        }

        public async Task SaveCommand(CustomCommands customCommand)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.CustomCommands.Update(customCommand);
                await db.SaveChangesAsync();
            }
        }

        public async Task SaveKeyword(KeywordType keyword)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Keywords.Update(keyword);
                await db.SaveChangesAsync();
            }
            await LoadCommands();
        }

        //To check for keywords
        private async Task OnChatMessage(object? sender, ChatMessageEventArgs e)
        {
            bool match = false;
            foreach (var keyword in Keywords)
            {
                if (IsCoolDownExpired(e.Sender, keyword.Keyword.CommandName) == false) continue;
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
                        Args = e.Message.Split(" ").ToList(),
                        IsWhisper = false,
                        Name = e.Sender,
                        DisplayName = e.DisplayName,
                        isSub = e.isSub,
                        isMod = e.isBroadcaster || e.isMod,
                        isVip = e.isVip,
                        isBroadcaster = e.isBroadcaster,
                    };
                    await processTagsAndSayMessage(commandEventArgs, keyword.Keyword.Response);
                    if (Commands[keyword.Keyword.CommandName].GlobalCooldown > 0)
                    {
                        AddGlobalCooldown(keyword.Keyword.CommandName, Commands[keyword.Keyword.CommandName].GlobalCooldown);
                    }
                    if (Commands[keyword.Keyword.CommandName].UserCooldown > 0)
                    {
                        AddCoolDown(e.Sender, keyword.Keyword.CommandName, Commands[keyword.Keyword.CommandName].UserCooldown);
                    }
                    break;
                }
            }

        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            if (e.Command.Equals("addcommand"))
            {
                if (!_serviceBackbone.IsBroadcasterOrBot(e.Name)) return;
                try
                {
                    var newCommand = JsonSerializer.Deserialize<CustomCommands>(e.Arg);
                    if (newCommand != null)
                    {
                        await AddCommand(newCommand);
                        await _serviceBackbone.SendChatMessage("Successfully added command");
                    }
                    else
                    {
                        await _serviceBackbone.SendChatMessage("failed to add command");
                    }

                }
                catch (Exception err)
                {
                    _logger.LogError(err, "Failed to add command");
                }

                return;
            }

            if (e.Command.Equals("refreshcommands"))
            {
                if (!_serviceBackbone.IsBroadcasterOrBot(e.Name)) return;
                await LoadCommands();
                return;
            }

            if (e.Command.Equals("disablecommand"))
            {
                if (!_serviceBackbone.IsBroadcasterOrBot(e.Name)) return;
                if (!Commands.ContainsKey(e.Arg)) return;
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var command = await db.CustomCommands.Where(x => x.CommandName.Equals(e.Arg)).FirstOrDefaultAsync();
                    if (command == null)
                    {
                        await _serviceBackbone.SendChatMessage(string.Format("Failed to disable {0}", e.Arg));
                        return;
                    }
                    command.Disabled = true;
                    Commands[e.Arg].Disabled = true;
                    db.Update(command);
                    await db.SaveChangesAsync();
                }
                await _serviceBackbone.SendChatMessage(string.Format("Disabled {0}", e.Arg));
                return;
            }

            if (e.Command.Equals("enablecommand"))
            {
                if (!_serviceBackbone.IsBroadcasterOrBot(e.Name)) return;
                if (!Commands.ContainsKey(e.Arg)) return;
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var command = await db.CustomCommands.Where(x => x.CommandName.Equals(e.Arg)).FirstOrDefaultAsync();
                    if (command == null)
                    {
                        await _serviceBackbone.SendChatMessage(string.Format("Failed to enable {0}", e.Arg));
                        return;
                    }
                    command.Disabled = false;
                    Commands[e.Arg].Disabled = false;
                    db.Update(command);
                    await db.SaveChangesAsync();
                }
                await _serviceBackbone.SendChatMessage(string.Format("Enabled {0}", e.Arg));
                return;
            }


            if (Commands.ContainsKey(e.Command))
            {
                if (Commands[e.Command].Disabled)
                {
                    return;
                }

                // if (!IsCoolDownExpired(e.Name, e.Command))
                // {
                //     await _serviceBackbone.SendChatMessage(e.DisplayName, string.Format("That command is still on cooldown: {0}", CooldownLeft(e.Name, e.Command)));
                //     return;
                // }

                if (!_serviceBackbone.IsBroadcasterOrBot(e.Name))
                {
                    switch (Commands[e.Command].MinimumRank)
                    {
                        case Rank.Viewer:
                            break; //everyone gets this
                        case Rank.Follower:
                            if (!(await _viewerFeature.IsFollower(e.Name)))
                            {
                                await SendChatMessage(e.DisplayName, "you must be a follower to use that command");
                                return;
                            }
                            break;
                        case Rank.Subscriber:
                            if (!(await _viewerFeature.IsSubscriber(e.Name)))
                            {
                                await SendChatMessage(e.DisplayName, "you must be a subscriber to use that command");
                                return;
                            }
                            break;
                        case Rank.Moderator:
                            if (!(await _viewerFeature.IsModerator(e.Name)))
                            {
                                await SendChatMessage(e.DisplayName, "only moderators can do that...");
                                return;
                            }
                            break;
                        case Rank.Streamer:
                            await SendChatMessage(e.DisplayName, "yeah ummm... no... go away");
                            return;
                    }
                }
                try
                {
                    await _semaphoreSlim.WaitAsync();

                    var isCoolDownExpired = await IsCoolDownExpiredWithMessage(e.Name, e.DisplayName, e.Command);
                    if (isCoolDownExpired == false) return;
                    if (Commands[e.Command].Cost > 0)
                    {
                        if ((await _loyaltyFeature.RemovePointsFromUser(e.Name, Commands[e.Command].Cost)) == false)
                        {
                            await _serviceBackbone.SendChatMessage(e.DisplayName, $"you don't have enough pasties, that command costs {Commands[e.Command].Cost}.");
                            return;
                        }
                    }

                    if (Commands[e.Command].GlobalCooldown > 0)
                    {
                        AddGlobalCooldown(e.Command, Commands[e.Command].GlobalCooldown);
                    }
                    if (Commands[e.Command].UserCooldown > 0)
                    {
                        AddCoolDown(e.Name, e.Command, Commands[e.Command].UserCooldown);
                    }
                }
                finally
                {
                    _semaphoreSlim.Release();
                }

                await processTagsAndSayMessage(e, Commands[e.Command].Response);
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

            var message = originalText;
            var mainRegex = new Regex(@"(?:[^\\]|^)(\(([^\\\s\|=()]*)([\s=\|](?:\\\(|\\\)|[^()])*)?\))", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            if (string.IsNullOrWhiteSpace(message)) return new CustomCommandResult();
            var cancel = false;
            while (true)
            {
                bool thisTagFound = false;
                var matches = mainRegex.Matches(message);
                if (matches.Count == 0) break;
                foreach (Match match in matches)
                {
                    thisTagFound = false;
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
                        message = message.Replace(wholeMatch.Value, "\\(" + wholeMatch.Value.Substring(1, wholeMatch.Value.Length - 2) + "\\)");
                    }

                }
                if (cancel) return new CustomCommandResult(cancel);
            }
            return new CustomCommandResult(message);
        }

        private async Task processTagsAndSayMessage(CommandEventArgs eventArgs, string commandText)
        {
            var mainRegex = new Regex(@"(?:[^\\]|^)(\(([^\\\s\|=()]*)([\s=\|](?:\\\(|\\\)|[^()])*)?\))", RegexOptions.Compiled | RegexOptions.IgnoreCase);
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
                    await _serviceBackbone.SendChatMessage(message);
                }
            }
        }


        public string UnescapeTagsInMessages(string args)
        {
            var regex = new Regex(@"\\([\\()])", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            //var result = regex.Replace(args,)
            var matches = regex.Matches(args);
            foreach (Match match in matches)
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
                _sendAlerts.QueueAlert(alertImage.Generate(args));
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
                if (eventArgs.isDiscord)
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
                var alertSound = new AlertSound()
                {
                    AudioHook = args
                };
                _sendAlerts.QueueAlert(alertSound);
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
            if (parseResults.Count() < 3) return new CustomCommandResult(args);
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

        private async Task<CustomCommandResult> CustomApiNoResponse(CommandEventArgs eventArgs, string args)
        {
            var httpClient = new HttpClient();
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri(args),
                Method = HttpMethod.Get
            };
            request.Headers.Add("Accept", "text/plain");
            var result = await httpClient.SendAsync(request);
            return new CustomCommandResult();
        }

        private async Task<CustomCommandResult> OnlineOnly(CommandEventArgs eventArgs, string args)
        {
            return await Task.Run(() =>
            {
                return _serviceBackbone.IsOnline ? new CustomCommandResult() : new CustomCommandResult(true);
            });
        }

        private async Task<CustomCommandResult> OfflineOnly(CommandEventArgs eventArgs, string args)
        {
            return await Task.Run(() =>
            {
                return _serviceBackbone.IsOnline ? new CustomCommandResult(true) : new CustomCommandResult();
            });
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
            var counterRegex = new Regex(@"^(\S+)(?:\s(.*))?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var match = counterRegex.Match(args);
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
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var counter = await db.Counters.Where(x => x.CounterName.Equals(counterName)).FirstOrDefaultAsync();
                if (counter == null)
                {
                    counter = new Counter()
                    {
                        CounterName = counterName,
                        Amount = 0
                    };
                    await db.Counters.AddAsync(counter);
                }
                if (eventArgs.Args.Count > 0 && (eventArgs.isBroadcaster || eventArgs.isMod))
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
                _sendAlerts.QueueAlert(alertImage.Generate(counterAlert));
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
    }
}