using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Commands.Misc;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;


namespace DotNetTwitchBot.Bot.Commands.TicketGames
{
    public class ModSpam : BaseCommandService
    {
        private AddActive _addActive;
        Timer _intervalTimer;
        private readonly ILogger<ModSpam> _logger;
        TimeSpan _runTime = new TimeSpan(0, 0, 0, 15);
        DateTime _startTime = new DateTime();

        public ModSpam(
            AddActive addActive,
            ServiceBackbone serviceBackbone,
            IServiceScopeFactory scopeFactory,
            CommandHandler commandHandler,
            ILogger<ModSpam> logger
            ) : base(serviceBackbone, scopeFactory, commandHandler)
        {
            _addActive = addActive;
            _intervalTimer = new Timer(timerCallBack, this, Timeout.Infinite, Timeout.Infinite);
            _logger = logger;
        }

        public override async Task RegisterDefaultCommands()
        {
            var moduleName = "ModSpam";
            await RegisterDefaultCommand("modspam", this, moduleName, Rank.Moderator);
            _logger.LogInformation($"Registered commands for {moduleName}");
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = _commandHandler.GetCommand(e.Command);
            if (command == null) return;
            switch (command.CommandProperties.CommandName)
            {
                case "modspam":
                    if (e.IsModOrHigher())
                    {
                        var isCooldownExpired = await IsCoolDownExpiredWithMessage(e.Name, e.DisplayName, e.Command);
                        if (isCooldownExpired == false) return;
                        await StartModSpam();
                    }
                    break;
            }

        }

        private async void timerCallBack(object? state)
        {
            if (state == null) return;
            var modSpam = (ModSpam)state;
            await modSpam.UpdateOrStopSpam();
        }

        private async Task UpdateOrStopSpam()
        {
            _addActive.AddActiveTickets(Tools.RandomRange(1, 8));
            var elapsedTime = DateTime.Now - _startTime;
            if (elapsedTime > _runTime)
            {
                _intervalTimer.Change(Timeout.Infinite, Timeout.Infinite);
                await _serviceBackbone.SendChatMessage("Mod spam completed... tickets arriving soon.");


            }
        }

        private async Task StartModSpam()
        {
            await _serviceBackbone.SendChatMessage("Starting Mod Spam... please wait while it spams silently...");
            _runTime = new TimeSpan(0, 0, Tools.RandomRange(15, 20));
            _startTime = DateTime.Now;
            _intervalTimer.Change(1000, 1000);
            AddGlobalCooldown("modspam", 1200);
        }
    }
}