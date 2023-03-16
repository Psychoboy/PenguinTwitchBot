using System.ComponentModel.Design;
using System.Text.RegularExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Alerts;

namespace DotNetTwitchBot.Bot.Commands.Custom
{
    public class CustomCommand : BaseCommand
    {
        Dictionary<string, Func<CommandEventArgs, string, Task<CustomCommandResult>>> CommandTags = new Dictionary<string, Func<CommandEventArgs, string, Task<CustomCommandResult>>>();
        Dictionary<string, string> Commands = new Dictionary<string, string>();
        private SendAlerts _sendAlerts;

        public CustomCommand(
            SendAlerts sendAlerts,
            ServiceBackbone eventService) : base(eventService)
        {
            _sendAlerts = sendAlerts;

            //RegisterCommands Here
            CommandTags.Add("alert", Alert);
            CommandTags.Add("sender", Sender);
            CommandTags.Add("playsound", PlaySound);
            CommandTags.Add("useronly", UserOnly);
            CommandTags.Add("writefile", WriteFile);
            CommandTags.Add("currenttime", CurrentTime);

            //Temporary add Test Commands
            Commands.Add("testalert", "(alert bonghit.gif, 12, 1.0,color: white;font-size: 50px;font-family: Arial;width: 600px;word-wrap: break-word;-webkit-text-stroke-width: 1px;-webkit-text-stroke-color: black;text-shadow: black 1px 0 5px;,) sptvHype sptvHype sptvHype sptvHype");
            Commands.Add("testsender", "(sender), Aegis: Buy more UEE BONDS!");
            Commands.Add("testaudio", "(playsound AngryScottish)");
            Commands.Add("testuseronly", "(useronly Super_Penguin_Bot) Should filter to SPB only.");
            Commands.Add("testwritenew", "(writefile wheelspins.txt, false, ------------------------------)");
            Commands.Add("testwriteappend", "(writefile wheelspins.txt, true, append)");
            Commands.Add("testcurrenttime", "(currenttime)");
            Commands.Add("testmultiple", "(writefile redeems.txt, true, (currenttime) (sender) customsfx) Only this should be left");
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
            while (true)
            {
                var matches = mainRegex.Matches(message);
                if (matches.Count == 0) break;
                foreach (Match match in matches)
                {
                    var groups = match.Groups;
                    var wholeMatch = groups[1];
                    var tagName = groups[2];
                    var tagArgs = groups[3];

                    if (CommandTags.ContainsKey(tagName.Value.Trim()))
                    {
                        CustomCommandResult result = await CommandTags[tagName.Value.Trim()](eventArgs, tagArgs.Value.Trim());
                        if (result.Cancel)
                        {
                            cancel = true;
                            break;
                        }

                        message = ReplaceFirstOccurrence(message, wholeMatch.Value, result.Message);
                    }
                }
                if (cancel) break;
            }
            if (cancel) return;

            if (!string.IsNullOrWhiteSpace(message))
            {
                await _eventService.SendChatMessage(message);
            }
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
    }
}