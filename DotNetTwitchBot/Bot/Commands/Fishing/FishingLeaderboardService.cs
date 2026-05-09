using DotNetTwitchBot.Bot.Core.Database;
using DotNetTwitchBot.Bot.Models.Fishing;
using DotNetTwitchBot.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetTwitchBot.Bot.Commands.Fishing
{
    public class FishingLeaderboardService : IFishingLeaderboardService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<FishingLeaderboardService> _logger;

        public FishingLeaderboardService(IServiceScopeFactory scopeFactory, ILogger<FishingLeaderboardService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task<List<LeaderPosition>> GetTotalGoldLeaderboard(int count = 50)
        {
            count = Math.Max(1, Math.Min(count, 1000)); // Clamp between 1 and 1000

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Use the most recent username for display
            var topPlayers = await context.FishCatches
                .AsNoTracking()
                .GroupBy(c => c.UserId)
                .Select(g => new 
                { 
                    UserId = g.Key,
                    g.OrderByDescending(c => c.CaughtAt).First().Username,
                    TotalGoldEarned = g.Sum(c => c.GoldEarned) 
                })
                .OrderByDescending(g => g.TotalGoldEarned)
                .Take(count)
                .ToListAsync();

            var leaderboard = topPlayers.Select((player, index) => new LeaderPosition
            {
                Rank = index + 1,
                Name = player.Username,
                Amount = player.TotalGoldEarned
            }).ToList();

            return leaderboard;
        }

        public async Task<List<SnapLossLeaderboardPosition>> GetSnapLossLeaderboard(int count = 50)
        {
            count = Math.Max(1, Math.Min(count, 1000)); // Clamp between 1 and 1000

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var topPlayers = await context.FishingSnapEvents
                .AsNoTracking()
                .GroupBy(e => e.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    g.OrderByDescending(e => e.SnappedAt).First().Username,
                    TotalGoldLost = g.Sum(e => e.TotalGoldLost),
                    TotalItemsLost = g.Sum(e => e.LostItemCount),
                    SnapCount = g.Count()
                })
                .OrderByDescending(g => g.TotalGoldLost)
                .ThenByDescending(g => g.TotalItemsLost)
                .Take(count)
                .ToListAsync();

            var leaderboard = topPlayers.Select((player, index) => new SnapLossLeaderboardPosition
            {
                Rank = index + 1,
                Name = player.Username,
                TotalGoldLost = player.TotalGoldLost,
                TotalItemsLost = player.TotalItemsLost,
                SnapCount = player.SnapCount
            }).ToList();

            return leaderboard;
        }

        public async Task<List<FishCatch>> GetMostValuableCatchesLeaderboard(int count = 50)
        {
            count = Math.Max(1, Math.Min(count, 1000)); // Clamp between 1 and 1000

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Get top catches by gold earned
            var topCatches = await context.FishCatches
                .Include(c => c.FishType)
                .OrderByDescending(c => c.GoldEarned)
                .ThenByDescending(c => c.CaughtAt)
                .Take(count)
                .ToListAsync();

            return topCatches;
        }

        public async Task<List<FishCatch>> GetRecentCatches(int count = 50)
        {
            count = Math.Max(1, Math.Min(count, 1000)); // Clamp between 1 and 1000

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Get most recent catches globally
            var recentCatches = await context.FishCatches
                .Include(c => c.FishType)
                .OrderByDescending(c => c.CaughtAt)
                .Take(count)
                .ToListAsync();

            return recentCatches;
        }

        public async Task<List<FishCatch>> GetUserRecentCatches(string userId, int count = 50)
        {
            count = Math.Max(1, Math.Min(count, 1000)); // Clamp between 1 and 1000

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Get most recent catches for specific user
            var userCatches = await context.FishCatches
                .Include(c => c.FishType)
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CaughtAt)
                .Take(count)
                .ToListAsync();

            return userCatches;
        }
    }
}
