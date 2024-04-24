using DotNetTwitchBot.Bot.TwitchServices;

namespace DotNetTwitchBot.Bot.StreamSchedule
{
    public class Schedule(ITwitchService twitchService) : ISchedule
    {
        public async Task<List<ScheduledStream>> GetNextStreams()
        {
            var result = await twitchService.GetStreamSchedule();
            if (result == null)
            {
                return [];
            }
            var streams = new List<ScheduledStream>();
            foreach (var stream in result.Segments)
            {
                if (stream.CanceledUntil != null) continue;
                streams.Add(new ScheduledStream { Start = stream.StartTime, End = stream.EndTime, Title = stream.Title });
                if (streams.Count >= 5) break;
            }
            return streams;
        }
    }
}
