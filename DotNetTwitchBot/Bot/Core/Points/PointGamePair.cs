using DotNetTwitchBot.Bot.Models.Games;
using DotNetTwitchBot.Bot.Models.Points;

namespace DotNetTwitchBot.Bot.Core.Points
{
    public class PointGamePair
    {
        public GameSetting Setting { get; set; } = null!;
        public PointType PointType { get; set; } = null!;
    }
}
