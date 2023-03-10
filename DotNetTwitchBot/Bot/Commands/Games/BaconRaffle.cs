using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;

namespace DotNetTwitchBot.Bot.Commands.Games
{
    public class BaconRaffle : BaseRaffle
    {
        Random _randNumber = new Random();
        public BaconRaffle(ServiceBackbone eventService, PointsFeature pointsFeature) : base(eventService, pointsFeature, "sptvBacon", "!bacon", "bacon")
        {
        }

        protected override void UpdateNumberOfWinners() {
            var entries = NumberEntered;
            var max = 3;
            var min = 1;
            switch(entries) {
                case <10:
                    break;
                case >= 10 and < 15:
                    max = 4;
                    break;
                case >=15 and < 20:
                    min = 2;
                    break;
                case >=20:
                    max = 5;
                    break;
            }
            NumberOfWinners = _randNumber.Next(min, max);
        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            switch (e.Command) {
                case "testbaconstart": {
                    if(e.Args.Count == 0) return;
                    if(!_eventService.IsBroadcasterOrBot(e.Sender)) return;
                    if(Int32.TryParse(e.Args[0], out int amount)) {
                        await StartRaffle(e.Sender, amount);
                    }
                }
                break;

                case "bacon": {
                    await EnterRaffle(e.Sender);
                }
                break;
            }
        }
    }
}