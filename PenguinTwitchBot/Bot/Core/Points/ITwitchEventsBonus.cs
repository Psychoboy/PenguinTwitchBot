using PenguinTwitchBot.Database.Bot.Models.Points;

namespace PenguinTwitchBot.Bot.Core.Points
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