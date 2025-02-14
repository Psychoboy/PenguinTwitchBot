using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class LastSeen : BaseCommandService, IHostedService
    {
        private readonly IViewerFeature _viewerFeature;
        private readonly ILogger<LastSeen> _logger;

        public LastSeen(
            ILogger<LastSeen> logger,
            IViewerFeature viewerFeature,
            IServiceBackbone serviceBackbone,
            ICommandHandler commandHandler
            ) : base(serviceBackbone, commandHandler, "LastSeen")
        {
            _viewerFeature = viewerFeature;
            _logger = logger;
        }

        public override async Task Register()
        {
            var moduleName = "LastSeen";
            await RegisterDefaultCommand("lastseen", this, moduleName);
            _logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            if (!command.CommandProperties.CommandName.Equals("lastseen")) return;

            if (string.IsNullOrWhiteSpace(e.TargetUser)) throw new SkipCooldownException();

            var viewer = await _viewerFeature.GetViewerByUserName(e.TargetUser);
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