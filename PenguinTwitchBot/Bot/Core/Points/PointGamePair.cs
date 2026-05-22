using PenguinTwitchBot.Bot.Models.Games;
using PenguinTwitchBot.Bot.Models.Points;

namespace PenguinTwitchBot.Bot.Core.Points
{
    public class PointGamePair
    {
        public GameSetting Setting { get; set; } = null!;
        public PointType PointType { get; set; } = null!;
    }
}
