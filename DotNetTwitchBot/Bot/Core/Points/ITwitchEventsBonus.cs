using DotNetTwitchBot.Bot.Models.Points;

namespace DotNetTwitchBot.Bot.Core.Points
{
    public interface ITwitchEventsBonus
    {
        Task<int> GetBitsPerPoint();
        Task<int> GetPointsPerSub();
        Task<PointType> GetPointType();
        Task SetBitsPerPoint(int numberOfBitsPerSub);
        Task SetPointsPerSub(int numberOfPointsPerSub);
        Task SetPointType(PointType pointType);
    }
}