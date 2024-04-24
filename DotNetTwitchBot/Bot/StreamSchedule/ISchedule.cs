
namespace DotNetTwitchBot.Bot.StreamSchedule
{
    public interface ISchedule
    {
        Task<List<ScheduledStream>> GetNextStreams();
    }
}