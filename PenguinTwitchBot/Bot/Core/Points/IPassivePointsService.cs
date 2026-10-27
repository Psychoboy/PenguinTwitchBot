using PenguinTwitchBot.Database.Bot.Models.Points;

namespace PenguinTwitchBot.Bot.Core.Points
{
    public interface IPassivePointsService
    {
        Task ProcessTickAsync(CancellationToken cancellationToken = default);
        Task DistributeForPointTypeAsync(PointType pointType, CancellationToken cancellationToken = default);
        void ResetScheduleForPointType(Guid pointTypeId);
    }
}

