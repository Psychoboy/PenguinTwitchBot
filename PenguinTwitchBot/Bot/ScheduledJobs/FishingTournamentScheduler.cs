using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Database.Bot.Core.Database;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.ScheduledJobs
{
    public class FishingTournamentScheduler(
        IServiceScopeFactory scopeFactory,
        ILogger<FishingTournamentScheduler> logger,
        IServiceBackbone? serviceBackbone = null,
        IFishingMessageSettingsService? messageSettingsService = null) : BackgroundService
    {
        private readonly ConcurrentDictionary<Guid, bool> _twoMinuteAnnounced = new();
        private readonly ConcurrentDictionary<Guid, bool> _thirtySecondAnnounced = new();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));

            await ProcessTournaments(stoppingToken);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ProcessTournaments(stoppingToken);
            }
        }

        private async Task ProcessTournaments(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var fishingService = scope.ServiceProvider.GetRequiredService<IFishingService>();

                var now = DateTime.UtcNow;

                var tournamentsToStart = await db.FishingTournaments
                    .Where(t => t.Enabled && t.Status == Database.Bot.Models.Fishing.FishingTournamentStatus.Scheduled && t.StartsAtUtc.HasValue && t.StartsAtUtc <= now)
                    .Select(t => t.Id)
                    .ToListAsync(stoppingToken);

                foreach (var tournamentId in tournamentsToStart)
                {
                    try
                    {
                        await fishingService.StartFishingTournament(tournamentId);
                        logger.LogInformation("Automatically started fishing tournament {TournamentId}", tournamentId);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to automatically start fishing tournament {TournamentId}", tournamentId);
                    }
                }

                var tournamentsToEnd = await db.FishingTournaments
                    .Where(t => t.Enabled && t.Status == Database.Bot.Models.Fishing.FishingTournamentStatus.Active && t.EndsAtUtc.HasValue && t.EndsAtUtc <= now)
                    .Select(t => t.Id)
                    .ToListAsync(stoppingToken);

                foreach (var tournamentId in tournamentsToEnd)
                {
                    try
                    {
                        var latestStatus = await db.FishingTournaments
                            .AsNoTracking()
                            .Where(t => t.Id == tournamentId)
                            .Select(t => (Database.Bot.Models.Fishing.FishingTournamentStatus?)t.Status)
                            .FirstOrDefaultAsync(stoppingToken);

                        // Tournament state can change after we build tournamentsToEnd.
                        // Re-checking avoids no-op calls being logged as automatic ends.
                        if (latestStatus != Database.Bot.Models.Fishing.FishingTournamentStatus.Active)
                        {
                            logger.LogDebug(
                                "Skipping auto-end for fishing tournament {TournamentId} because current status is {Status}",
                                tournamentId,
                                latestStatus?.ToString() ?? "Missing");
                            continue;
                        }

                        await fishingService.EndFishingTournament(tournamentId);
                        _twoMinuteAnnounced.TryRemove(tournamentId, out _);
                        _thirtySecondAnnounced.TryRemove(tournamentId, out _);
                        logger.LogInformation("Automatically ended fishing tournament {TournamentId}", tournamentId);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to automatically end fishing tournament {TournamentId}", tournamentId);
                    }
                }

                // Final-sprint countdown hype for short/blitz tournaments (<= 30 minutes)
                if (serviceBackbone != null)
                {
                    var settings = await fishingService.GetSettings();
                    if (settings?.AnnounceTournamentCountdown == true)
                    {
                        var activeShortTournaments = await db.FishingTournaments
                            .AsNoTracking()
                            .Where(t => t.Enabled
                                && t.Status == Database.Bot.Models.Fishing.FishingTournamentStatus.Active
                                && t.RunDurationMinutes <= 30
                                && t.EndsAtUtc.HasValue
                                && t.EndsAtUtc > now)
                            .Select(t => new { t.Id, t.Name, t.EndsAtUtc })
                            .ToListAsync(stoppingToken);

                        var msgs = messageSettingsService != null ? await messageSettingsService.GetAsync() : new FishingMessageSettings();
                        foreach (var t in activeShortTournaments)
                        {
                            var remainingSeconds = (t.EndsAtUtc!.Value - now).TotalSeconds;
                            if (remainingSeconds <= 35 && remainingSeconds > 0 && _thirtySecondAnnounced.TryAdd(t.Id, true))
                            {
                                var top = (await fishingService.GetFishingTournamentStandings(t.Id, 1)).FirstOrDefault();
                                var leaderNote = top != null ? $" Current leader: {top.Username} ({top.Score:N2})!" : "";
                                var msg = FishingMessageSettings.Format(msgs.TournamentThirtySecondWarning, ("tournament", t.Name), ("leaderNote", leaderNote));
                                if (!string.IsNullOrWhiteSpace(msg)) await serviceBackbone.SendChatMessage(msg);
                            }
                            else if (remainingSeconds <= 125 && remainingSeconds > 40 && _twoMinuteAnnounced.TryAdd(t.Id, true))
                            {
                                var top = (await fishingService.GetFishingTournamentStandings(t.Id, 1)).FirstOrDefault();
                                var leaderNote = top != null ? $" Current leader: {top.Username} ({top.Score:N2})!" : "";
                                var msg = FishingMessageSettings.Format(msgs.TournamentTwoMinuteWarning, ("tournament", t.Name), ("leaderNote", leaderNote));
                                if (!string.IsNullOrWhiteSpace(msg)) await serviceBackbone.SendChatMessage(msg);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process fishing tournaments in scheduler cycle");
            }
        }
    }
}