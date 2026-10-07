

using PenguinTwitchBot.Database.Bot.Models.IpLogs;
using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class IpLogRepository(ApplicationDbContext context) : GenericRepository<IpLogEntry>(context), IIpLogRepository
    {
        public async Task<List<IpLogEntry>> GetDuplicateIpsForUser(string username, string? userId = null, int? limit = null, int? offset = null)
        {
            var baseQuery = _context.IpLogEntrys.AsQueryable();
            if (!string.IsNullOrEmpty(userId))
            {
                baseQuery = baseQuery.Where(x => x.UserId == userId);
            }
            else
            {
                baseQuery = baseQuery.Where(x => x.Username.Equals(username));
            }
            var userIps = baseQuery.Select(y => y.Ip).Distinct();

            var query = _context.IpLogEntrys
                .Where(x => userIps.Contains(x.Ip));

            if (!string.IsNullOrEmpty(userId))
            {
                query = query.Where(x => x.UserId != userId);
            }
            else
            {
                query = query.Where(x => x.Username.Equals(username) == false);
            }

            if (offset != null)
            {
                query = query.Skip((int)offset);
            }

            if (limit != null)
            {
                query = query.Take((int)limit);
            }

            return await query.ToListAsync();
        }

        public async Task<List<IpLogUsersWithSameIp>> GetAllUsersWithDuplicateIps()
        {
            // Fetch records with Ip, UserId, Username, and ConnectedDate.
            var ipUserPairs = await _context.IpLogEntrys
                .Where(x => !string.IsNullOrEmpty(x.UserId))
                .Select(x => new { x.Ip, x.UserId, x.Username, x.ConnectedDate })
                .ToListAsync();

            return ipUserPairs
                .GroupBy(x => x.Ip)
                .Select(g =>
                {
                    // Group by UserId so the same user (even if recorded under older usernames) is a single distinct user.
                    // Pick the most recent username for display.
                    var distinctUsers = g
                        .GroupBy(u => u.UserId)
                        .Select(ug => ug.OrderByDescending(u => u.ConnectedDate).First())
                        .OrderBy(u => u.Username, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var pairs = new List<IpLogUsersWithSameIp>();
                    for (var i = 0; i < distinctUsers.Count; i++)
                    {
                        for (var j = i + 1; j < distinctUsers.Count; j++)
                        {
                            pairs.Add(new IpLogUsersWithSameIp
                            {
                                User1 = distinctUsers[i].Username,
                                User2 = distinctUsers[j].Username
                            });
                        }
                    }
                    return pairs;
                })
                .SelectMany(pairs => pairs)
                .DistinctBy(x => (x.User1.ToLowerInvariant(), x.User2.ToLowerInvariant()))
                .OrderBy(x => x.User1, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.User2, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<List<IpLogEntry>> GetKnownIpsForUser(string username, string? userId = null, int? limit = null, int? offset = null)
        {
            var query = _context.IpLogEntrys.AsQueryable();
            if (!string.IsNullOrEmpty(userId))
            {
                query = query.Where(x => x.UserId == userId);
            }
            else
            {
                query = query.Where(x => x.Username == username);
            }

            if (offset != null)
            {
                query = query.Skip((int)offset);
            }

            if (limit != null)
            {
                query = query.Take((int)limit);
            }

            return await query.ToListAsync();
        }
    }
}
