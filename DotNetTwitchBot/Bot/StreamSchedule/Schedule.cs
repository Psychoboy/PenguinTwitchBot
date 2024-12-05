using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.TwitchServices;
using DotNetTwitchBot.Repository;
using TwitchLib.Api.Helix.Models.Schedule;

namespace DotNetTwitchBot.Bot.StreamSchedule
{
    public class Schedule(ITwitchService twitchService, IDiscordService discordService, IServiceScopeFactory scopeFactory, ILogger<Schedule> logger) : ISchedule
    {
        public async Task<List<ScheduledStream>> GetNextStreams()
        {
            var result = await twitchService.GetStreamSchedule();
            if (result == null)
            {
                return [];
            }
            var vacation = result.Vacation;
            var streams = new List<ScheduledStream>();
            foreach (var stream in result.Segments)
            {
                if (stream.CanceledUntil.HasValue) continue;
                if(vacation != null && vacation.StartTime < stream.StartTime && vacation.EndTime > stream.EndTime) continue;
                streams.Add(new ScheduledStream { Start = stream.StartTime, End = stream.EndTime, Title = stream.Title });
            }
            return streams;
        }

        public async Task UpdateEvents()
        {
            var result = await twitchService.GetStreamSchedule();
            if (result == null) return;
            var anyUpdates = false;
            var foundEvents = new List<ulong>();
            var vacation = result.Vacation;
            foreach (var stream in result.Segments.Where(x => x.StartTime < DateTime.UtcNow.AddDays(7) || x.IsRecurring == false))
            {
                if (stream.CanceledUntil != null) continue;
                if (vacation != null && vacation.StartTime < stream.StartTime && vacation.EndTime > stream.EndTime) continue;
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var twitchDiscordEvent = await db.DiscordTwitchEventMap.Find(x => x.TwitchEventId.Equals(stream.Id)).FirstOrDefaultAsync();

                if (twitchDiscordEvent != null)
                {
                    var discordEvent = await discordService.GetEvent(twitchDiscordEvent.DiscordEventId);
                    if (discordEvent == null)
                    {
                        db.DiscordTwitchEventMap.Remove(twitchDiscordEvent);

                        await db.SaveChangesAsync();
                        logger.LogInformation("Discord event didn't exist so removing mapping to get on next cycle.");
                    }
                    else
                    {
                        foundEvents.Add(discordEvent.Id);
                        if (discordEvent.Name.Equals(stream.Title) == false ||
                            discordEvent.StartTime.ToUniversalTime().Equals(stream.StartTime) == false)
                        {
                            logger.LogInformation("Updating discord event {title}", stream.Title);
                            await discordService.UpdateEvent(discordEvent, stream.Title, stream.StartTime, stream.EndTime);
                            anyUpdates = true;
                        }

                    }
                }
                else
                {
                    ulong discordEventId = await CreateEvent(stream);
                    if (discordEventId == 0) continue;
                    logger.LogInformation("Added discord event {title}", stream.Title);
                    await db.DiscordTwitchEventMap.AddAsync(new DiscordEventMap
                    {
                        DiscordEventId = discordEventId,
                        TwitchEventId = stream.Id
                    });
                    foundEvents.Add(discordEventId);
                    await db.SaveChangesAsync();
                    anyUpdates = true;
                }
            }

            var discordEvents = await discordService.GetEvents();
            var shouldBeDeletedEvents = discordEvents.Where(x => foundEvents.Contains(x.Id) == false).ToList();
            var connectedId = discordService.GetConnectedAsId();
            if (connectedId != 0)
            {
                shouldBeDeletedEvents = discordEvents.Where(x => x.Creator.Id == connectedId).ToList();
                foreach (var shouldDeleteEvent in shouldBeDeletedEvents)
                {
                    if (shouldDeleteEvent.StartTime.ToUniversalTime() < DateTime.UtcNow) continue;
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var scheduledEvent = db.DiscordTwitchEventMap.Find(x => x.DiscordEventId == shouldDeleteEvent.Id);
                    if (scheduledEvent != null)
                    {
                        await discordService.DeleteEvent(shouldDeleteEvent);
                        anyUpdates = true;
                    }
                }
            }

            if (anyUpdates)
            {
                logger.LogInformation("Updating posted schedule.");
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var lastSchedule = await db.Settings.Find(x => x.Name.Equals("LastPostedSchedule")).FirstOrDefaultAsync();
                if (lastSchedule == null) return;
                var endDate = new DateTime(lastSchedule.LongSetting);
                var nextStreams = (await GetNextStreams()).Where(x => x.Start < endDate).ToList();
                var lastScheduleId = ulong.Parse(lastSchedule.StringSetting);
                await discordService.UpdatePostedSchedule(lastScheduleId, nextStreams);
            }
        }

        public async Task PostSchedule()
        {
            var streams = (await GetNextStreams()).FindAll(x => x.End < DateTime.Now.AddDays(7));
            var id = await discordService.PostSchedule(streams);
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var lastSchedule = await db.Settings.Find(x => x.Name.Equals("LastPostedSchedule")).FirstOrDefaultAsync();
            if (lastSchedule != null)
            {
                if (ulong.TryParse(lastSchedule.StringSetting, out var lastId))
                {
                    await discordService.DeletePostedScheduled(lastId);
                }
                lastSchedule.StringSetting = id.ToString();
                lastSchedule.LongSetting = DateTime.Now.AddDays(7).Ticks;
                db.Settings.Update(lastSchedule);
                await db.SaveChangesAsync();
            }
            else
            {
                lastSchedule = new Setting { Name = "LastPostedSchedule", DataType = Setting.DataTypeEnum.String, StringSetting = id.ToString(), LongSetting = DateTime.Now.AddDays(7).Ticks };
                await db.Settings.AddAsync(lastSchedule);
                await db.SaveChangesAsync();
            }
        }

        private async Task<ulong> CreateEvent(Segment stream)
        {
            //Check if event exists
            var existingDiscordEvents = await discordService.GetEvents();
            foreach (var existingEvent in existingDiscordEvents)
            {
                if (existingEvent != null)
                {
                    if (existingEvent.StartTime.Equals(stream.StartTime) && existingEvent.Name.Equals(stream.Title))
                        return existingEvent.Id;
                }
            }

            if (stream.IsRecurring == false)
            {
                //Create one off
                return await discordService.CreateScheduledEvent(new ScheduledStream { Start = stream.StartTime, End = stream.EndTime, Title = stream.Title });
            }
            else if (stream.StartTime > DateTime.UtcNow.AddDays(7))
            {
                return (ulong)0;
            }
            else
            {
                return await discordService.CreateScheduledEvent(new ScheduledStream { Start = stream.StartTime, End = stream.EndTime, Title = stream.Title });
            }
        }
    }
}
