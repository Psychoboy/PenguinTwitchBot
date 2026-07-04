using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Database.Bot.Core.Database;

namespace PenguinTwitchBot.Bot.ScheduledJobs
{
    public class FishingTournamentScheduler(IServiceScopeFactory scopeFactory, ILogger<FishingTournamentScheduler> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

            await ProcessTournaments(stoppingToken);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ProcessTournaments(stoppingToken);
            }
        }

        private async Task ProcessTournaments(CancellationToken stoppingToken)
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
                    await fishingService.EndFishingTournament(tournamentId);
                    logger.LogInformation("Automatically ended fishing tournament {TournamentId}", tournamentId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to automatically end fishing tournament {TournamentId}", tournamentId);
                }
            }
        }
    }
}