using Discord;
using PenguinTwitchBot.Bot.Models;
using PenguinTwitchBot.Bot.StreamSchedule;

namespace PenguinTwitchBot.Bot.Core
{
    public interface IDiscordService
    {
        Task<ulong> CreateScheduledEvent(ScheduledStream scheduledStream);
        Task DeleteEvent(IGuildScheduledEvent evt);
        Task DeletePostedScheduled(ulong id);
        ulong GetConnectedAsId();
        Task<IGuildScheduledEvent?> GetEvent(ulong id);
        Task<IReadOnlyCollection<IGuildScheduledEvent>> GetEvents();
        IReadOnlyList<DiscordGuildInfo> GetCachedGuilds();
        IReadOnlyList<DiscordChannelInfo> GetCachedTextChannels(ulong guildId);
        IReadOnlyList<DiscordRoleInfo> GetCachedRoles(ulong guildId);
        Task LogAsync(LogMessage message);
        Task<ulong> PostSchedule(List<ScheduledStream> scheduledStreams);
        Task RestartAsync(CancellationToken cancellationToken = default);
        ConnectionState ServiceStatus();
        void SetReady(bool ready);
        Task UpdateEvent(IGuildScheduledEvent evt, string title, DateTime startTime, DateTime endTime);
        Task UpdatePostedSchedule(ulong id, List<ScheduledStream> scheduledStreams);
        Task UserStreaming(IGuildUser user, bool isStreaming);
    }
}