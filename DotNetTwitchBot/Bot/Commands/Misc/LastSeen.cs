using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class LastSeen : BaseCommandService
    {
        private ViewerFeature _viewerFeature;
        private ILogger<LastSeen> _logger;

        public LastSeen(
            ILogger<LastSeen> logger,
            ViewerFeature viewerFeature,
            ServiceBackbone serviceBackbone,
            IServiceScopeFactory scopeFactory,
            CommandHandler commandHandler
            ) : base(serviceBackbone, scopeFactory, commandHandler)
        {
            _viewerFeature = viewerFeature;
            _logger = logger;
        }

        public override async Task RegisterDefaultCommands()
        {
            var moduleName = "LastSeen";
            await RegisterDefaultCommand("lastseen", this, moduleName);
            _logger.LogInformation($"Registered commands for {moduleName}");
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = _commandHandler.GetCommand(e.Command);
            if (command == null) return;
            if (!command.CommandProperties.CommandName.Equals("lastseen")) return;

            var isCoolDownExpired = await IsCoolDownExpiredWithMessage(e.Name, e.DisplayName, e.Command);
            if (isCoolDownExpired == false) return;
            if (string.IsNullOrWhiteSpace(e.TargetUser)) return;

            var viewer = await _viewerFeature.GetViewer(e.TargetUser);
            if (viewer != null && viewer.LastSeen != DateTime.MinValue)
            {
                var seconds = Convert.ToInt32((DateTime.Now - viewer.LastSeen).TotalSeconds);
                await SendChatMessage(e.DisplayName, $"{viewer.NameWithTitle()} was last seen {Tools.ConvertToCompoundDuration(seconds)} ago");
            }
            else
            {
                await SendChatMessage(e.DisplayName, string.Format("Have never seen {0}", e.Arg));
            }
        }
    }
}