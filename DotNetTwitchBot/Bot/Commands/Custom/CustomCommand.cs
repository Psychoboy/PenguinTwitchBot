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

namespace DotNetTwitchBot.Bot.Commands.Custom
{
    public class CustomCommand : BaseCommand
    {
        Dictionary<string, Func<CommandEventArgs, string, Task<CustomCommandResult>>> CommandTags = new Dictionary<string, Func<CommandEventArgs, string, Task<CustomCommandResult>>>();
        Dictionary<string, string> Commands = new Dictionary<string, string>();
        private SendAlerts _sendAlerts;
        private ViewerFeature _viewerFeature;

        public CustomCommand(
            SendAlerts sendAlerts,
            ViewerFeature viewerFeature,
            ServiceBackbone eventService) : base(eventService)
        {
            _sendAlerts = sendAlerts;
            _viewerFeature = viewerFeature;

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

            //Temporary add Test Commands
            Commands.Add("testalert", "(alert bonghit.gif, 12, 1.0,color: white;font-size: 50px;font-family: Arial;width: 600px;word-wrap: break-word;-webkit-text-stroke-width: 1px;-webkit-text-stroke-color: black;text-shadow: black 1px 0 5px;,) sptvHype sptvHype sptvHype sptvHype");
            Commands.Add("testsender", "(sender), Aegis: Buy more UEE BONDS!");
            Commands.Add("testaudio", "(playsound AngryScottish)");
            Commands.Add("testuseronly", "(useronly Super_Penguin_Bot) Should filter to SPB only.");
            Commands.Add("testwritenew", "(writefile wheelspins.txt, false, ------------------------------)");
            Commands.Add("testwriteappend", "(writefile wheelspins.txt, true, append)");
            Commands.Add("testcurrenttime", "(currenttime)");
            Commands.Add("testmultiple", "(writefile redeems.txt, true, (currenttime) (sender) customsfx) Only this should be left");
            Commands.Add("testapitext", "(sender), (customapitext https://icanhazdadjoke.com/)");
            Commands.Add("testfollowage", "(followage)");
        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            if (Commands.ContainsKey(e.Command))
            {
                await processTagsAndSayMessage(e, Commands[e.Command]);
            }
        }

        private async Task processTagsAndSayMessage(CommandEventArgs eventArgs, string commandText)
        {
            var message = commandText;
            var outMessage = message;
            var mainRegex = new Regex(@"(?:[^\\]|^)(\(([^\\\s\|=()]*)([\s=\|](?:\\\(|\\\)|[^()])*)?\))", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            bool cancel = false;
            bool thisTagFound = false;
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
    }
}