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
        private readonly AddActive _addActive;
        readonly Timer _intervalTimer;
        private readonly ILogger<ModSpam> _logger;
        TimeSpan _runTime = new(0, 0, 0, 15);
        DateTime _startTime = new();

        public ModSpam(
            AddActive addActive,
            ServiceBackbone serviceBackbone,
            CommandHandler commandHandler,
            ILogger<ModSpam> logger
            ) : base(serviceBackbone, commandHandler)
        {
            _addActive = addActive;
            _intervalTimer = new Timer(TimerCallBack, this, Timeout.Infinite, Timeout.Infinite);
            _logger = logger;
        }

        public override async Task Register()
        {
            var moduleName = "ModSpam";
            await RegisterDefaultCommand("modspam", this, moduleName, Rank.Moderator, globalCoolDown: 1200);
            _logger.LogInformation($"Registered commands for {moduleName}");
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            switch (command.CommandProperties.CommandName)
            {
                case "modspam":
                    await StartModSpam();
                    break;
            }

        }

        private async void TimerCallBack(object? state)
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
                await ServiceBackbone.SendChatMessage("Mod spam completed... tickets arriving soon.");


            }
        }

        private async Task StartModSpam()
        {
            await ServiceBackbone.SendChatMessage("Starting Mod Spam... please wait while it spams silently...");
            _runTime = new TimeSpan(0, 0, Tools.RandomRange(15, 20));
            _startTime = DateTime.Now;
            _intervalTimer.Change(1000, 1000);
        }
    }
}