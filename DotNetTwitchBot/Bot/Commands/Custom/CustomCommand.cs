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

namespace DotNetTwitchBot.Bot.Commands.Custom
{
    public class CustomCommand : BaseCommand
    {
        Dictionary<string, Func<CommandEventArgs, string, Task<CustomCommandResult>>> CommandTags = new Dictionary<string, Func<CommandEventArgs, string, Task<CustomCommandResult>>>();
        Dictionary<string, Models.CustomCommands> Commands = new Dictionary<string, Models.CustomCommands>();
        private SendAlerts _sendAlerts;
        private ViewerFeature _viewerFeature;
        private readonly IServiceScopeFactory _scopeFactory;
        private ILogger<CustomCommand> _logger;
        private TwitchService _twitchService;

        public CustomCommand(
            SendAlerts sendAlerts,
            ViewerFeature viewerFeature,
            IServiceScopeFactory scopeFactory,
            ILogger<CustomCommand> logger,
            TwitchService twitchService,
            ServiceBackbone eventService) : base(eventService)
        {
            _sendAlerts = sendAlerts;
            _viewerFeature = viewerFeature;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _twitchService = twitchService;

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



            //Temporary add Test Commands
            // Commands.Add("testalert", "(alert bonghit.gif, 12, 1.0,color: white;font-size: 50px;font-family: Arial;width: 600px;word-wrap: break-word;-webkit-text-stroke-width: 1px;-webkit-text-stroke-color: black;text-shadow: black 1px 0 5px;,) sptvHype sptvHype sptvHype sptvHype");
            // Commands.Add("testsender", "(sender), Aegis: Buy more UEE BONDS!");
            // Commands.Add("testaudio", "(playsound AngryScottish)");
            // Commands.Add("testuseronly", "(useronly Super_Penguin_Bot) Should filter to SPB only.");
            // Commands.Add("testwritenew", "(writefile wheelspins.txt, false, ------------------------------)");
            // Commands.Add("testwriteappend", "(writefile wheelspins.txt, true, append)");
            // Commands.Add("testcurrenttime", "(currenttime)");
            // Commands.Add("testmultiple", "(writefile redeems.txt, true, (currenttime) (sender) customsfx) Only this should be left");
            // Commands.Add("testapitext", "(sender), (customapitext https://icanhazdadjoke.com/)");
            // Commands.Add("testfollowage", "(followage)");
            // Commands.Add("testcounter", "There has been (multicounter pubcrawldeath) pub crawl deaths sptvDrink");
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

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            if (e.Command.Equals("addcommand"))
            {
                if (!_eventService.IsBroadcasterOrBot(e.Name)) return;
                try
                {
                    var newCommand = JsonSerializer.Deserialize<CustomCommands>(e.Arg);
                    if (newCommand != null)
                    {
                        await AddCommand(newCommand);
                    }
                    else
                    {
                        await _eventService.SendChatMessage("failed to add command");
                        return;
                    }
                    await _eventService.SendChatMessage("Successfully added command");
                    return;
                }
                catch (Exception err)
                {
                    _logger.LogError(err, "Failed to add command");
                }

                return;
            }

            if (e.Command.Equals("refreshcommands"))
            {
                if (!_eventService.IsBroadcasterOrBot(e.Name)) return;
                await LoadCommands();
                return;
            }

            if (e.Command.Equals("disablecommand"))
            {
                if (!_eventService.IsBroadcasterOrBot(e.Name)) return;
                if (!Commands.ContainsKey(e.Arg)) return;
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var command = await db.CustomCommands.Where(x => x.CommandName.Equals(e.Arg)).FirstOrDefaultAsync();
                    if (command == null)
                    {
                        await _eventService.SendChatMessage(string.Format("Failed to disable {0}", e.Arg));
                        return;
                    }
                    command.Disabled = true;
                    Commands[e.Arg].Disabled = true;
                    db.Update(command);
                    await db.SaveChangesAsync();
                }
                await _eventService.SendChatMessage(string.Format("Disabled {0}", e.Arg));
                return;
            }

            if (e.Command.Equals("enablecommand"))
            {
                if (!_eventService.IsBroadcasterOrBot(e.Name)) return;
                if (!Commands.ContainsKey(e.Arg)) return;
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var command = await db.CustomCommands.Where(x => x.CommandName.Equals(e.Arg)).FirstOrDefaultAsync();
                    if (command == null)
                    {
                        await _eventService.SendChatMessage(string.Format("Failed to enable {0}", e.Arg));
                        return;
                    }
                    command.Disabled = false;
                    Commands[e.Arg].Disabled = false;
                    db.Update(command);
                    await db.SaveChangesAsync();
                }
                await _eventService.SendChatMessage(string.Format("Enabled {0}", e.Arg));
                return;
            }


            if (Commands.ContainsKey(e.Command))
            {
                if (Commands[e.Command].Disabled)
                {
                    return;
                }

                if (!IsCoolDownExpired(e.Name, e.Command))
                {
                    await _eventService.SendChatMessage(e.DisplayName, "That command is still on cooldown");
                    return;
                }
                if (!_eventService.IsBroadcasterOrBot(e.Name))
                {
                    switch (Commands[e.Command].MinimumRank)
                    {
                        case CustomCommands.Rank.Viewer:
                            break; //everyone gets this
                        case CustomCommands.Rank.Follower:
                            if (!(await _viewerFeature.IsFollower(e.Name)))
                            {
                                await SendChatMessage(e.DisplayName, "you must be a follower to use that command");
                                return;
                            }
                            break;
                        case CustomCommands.Rank.Subscriber:
                            if (!(await _viewerFeature.IsSubscriber(e.Name)))
                            {
                                await SendChatMessage(e.DisplayName, "you must be a subscriber to use that command");
                                return;
                            }
                            break;
                        case CustomCommands.Rank.Moderator:
                            if (!(await _viewerFeature.IsModerator(e.Name)))
                            {
                                await SendChatMessage(e.DisplayName, "only moderators can do that...");
                                return;
                            }
                            break;
                        case CustomCommands.Rank.Streamer:
                            await SendChatMessage(e.DisplayName, "yeah ummm... no... go away");
                            return;
                    }
                }
            }

            await processTagsAndSayMessage(e, Commands[e.Command].Response);

            if (Commands[e.Command].GlobalCooldown > 0)
            {
                AddGlobalCooldown(e.Command, Commands[e.Command].GlobalCooldown);
            }
            if (Commands[e.Command].UserCooldown > 0)
            {
                AddCoolDown(e.Name, e.Command, Commands[e.Command].UserCooldown);
            }
        }

        private async Task processTagsAndSayMessage(CommandEventArgs eventArgs, string commandText)
        {
            // var message = commandText;
            // var outMessage = message;
            var mainRegex = new Regex(@"(?:[^\\]|^)(\(([^\\\s\|=()]*)([\s=\|](?:\\\(|\\\)|[^()])*)?\))", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            bool cancel = false;
            bool thisTagFound = false;
            var messages = commandText.Split("\n");
            foreach (var oldMessage in messages)
            {
                var message = oldMessage;
                if (string.IsNullOrWhiteSpace(message)) continue;
                while (true)
                {
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
                    if (cancel) break;

                }
                if (cancel) return;

                if (!string.IsNullOrWhiteSpace(message))
                {
                    message = UnescapeTags(message);
                    await _eventService.SendChatMessage(message);
                }
            }
        }


        private string UnescapeTags(string args)
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
                return new CustomCommandResult(string.Format("@{0}, ", eventArgs.DisplayName));
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
                return _eventService.IsOnline ? new CustomCommandResult() : new CustomCommandResult(true);
            });
        }

        private async Task<CustomCommandResult> OfflineOnly(CommandEventArgs eventArgs, string args)
        {
            return await Task.Run(() =>
            {
                return _eventService.IsOnline ? new CustomCommandResult(true) : new CustomCommandResult();
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
                }
                amount = counter.Amount;
                await db.SaveChangesAsync();

            }
            counterAlert = counterAlert.Replace("\\(totalcount\\)", amount.ToString());
            if (!string.IsNullOrWhiteSpace(counterAlert))
            {
                var alertImage = new AlertImage();
                _sendAlerts.QueueAlert(alertImage.Generate(counterAlert));
            }

            return new CustomCommandResult(amount.ToString());
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

        private async Task<CustomCommandResult> Uptime(CommandEventArgs eventArgs, string args)
        {
            var streamTime = await _twitchService.StreamStartedAt();
            if (streamTime == DateTime.MinValue) return new CustomCommandResult("Stream is offline");
            var currentTime = DateTime.Now;
            var totalTime = currentTime - streamTime;
            return new CustomCommandResult(totalTime.ToString());
        }
    }
}