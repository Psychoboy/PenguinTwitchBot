using Discord;
using DotNetTwitchBot.Bot.StreamSchedule;

namespace DotNetTwitchBot.Bot.Core
{
    public interface IDiscordService
    {
        Task<ulong> CreateScheduledEvent(ScheduledStream scheduledStream);
        Task DeleteEvent(IGuildScheduledEvent evt);
        Task DeletePostedScheduled(ulong id);
        Task<IGuildScheduledEvent> GetEvent(ulong id);
        Task<IReadOnlyCollection<IGuildScheduledEvent>> GetEvents();
        Task LogAsync(LogMessage message);
        Task<ulong> PostSchedule(List<ScheduledStream> scheduledStreams);
        ConnectionState ServiceStatus();
        Task UpdateEvent(IGuildScheduledEvent evt, string title, DateTime startTime, DateTime endTime);
        Task UpdatePostedSchedule(ulong id, List<ScheduledStream> scheduledStreams);
    }
}