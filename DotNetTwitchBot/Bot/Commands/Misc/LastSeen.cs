using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class LastSeen : BaseCommand
    {
        private ViewerFeature _viewerFeature;

        public LastSeen(
            ViewerFeature viewerFeature,
            ServiceBackbone serviceBackbone
            ) : base(serviceBackbone)
        {
            _viewerFeature = viewerFeature;
        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = "lastseen";
            if (!e.Command.Equals(command)) return;
            if (!IsCoolDownExpired(e.Name, command)) return;
            if (string.IsNullOrWhiteSpace(e.TargetUser)) return;

            var viewer = await _viewerFeature.GetViewer(e.TargetUser);
            if (viewer != null && viewer.LastSeen != DateTime.MinValue)
            {
                var seconds = Convert.ToInt32((DateTime.Now - viewer.LastSeen).TotalSeconds);
                await SendChatMessage(e.DisplayName, $"{viewer.DisplayName} was last seen {Tools.ConvertToCompoundDuration(seconds)} ago");
            }
            else
            {
                await SendChatMessage(e.DisplayName, string.Format("Have never seen {0}", e.Arg));
            }
        }
    }
}