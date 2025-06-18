using DotNetTwitchBot.Bot.Models.Points;

namespace DotNetTwitchBot.Bot.Core.Points
{
    public interface ITwitchEventsBonus
    {
        Task<double> GetBitsPerPoint();
        Task<int> GetPointsPerSub();
        Task<PointType> GetPointType();
        Task SetBitsPerPoint(double numberOfPointsPerBit);
        Task SetPointsPerSub(int numberOfPointsPerSub);
        Task SetPointType(PointType pointType);
    }
}