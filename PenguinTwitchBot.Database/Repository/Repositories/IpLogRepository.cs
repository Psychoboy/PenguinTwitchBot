

using PenguinTwitchBot.Bot.Models.IpLogs;
using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class IpLogRepository(ApplicationDbContext context) : GenericRepository<IpLogEntry>(context), IIpLogRepository
    {
        public async Task<List<IpLogEntry>> GetDuplicateIpsForUser(string username, int? limit = null, int? offset = null)
        {
            var baseQuery = _context.IpLogEntrys
                .Where(x => x.Username.Equals(username)).Select(y => y.Ip).Distinct();

            var query = _context.IpLogEntrys
                .Where(x => baseQuery.Contains(x.Ip) && x.Username.Equals(username) == false);

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
            // Fetch distinct (Ip, Username) pairs — much smaller than the full table.
            // Avoids an O(N²) self-join by doing pair generation in memory.
            var ipUserPairs = await _context.IpLogEntrys
                .Select(x => new { x.Ip, x.Username })
                .Distinct()
                .ToListAsync();

            return ipUserPairs
                .GroupBy(x => x.Ip)
                .Where(g => g.Count() > 1)
                .SelectMany(g =>
                {
                    var users = g.Select(x => x.Username).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
                    var pairs = new List<IpLogUsersWithSameIp>();
                    for (var i = 0; i < users.Count; i++)
                        for (var j = i + 1; j < users.Count; j++)
                            pairs.Add(new IpLogUsersWithSameIp { User1 = users[i], User2 = users[j] });
                    return pairs;
                })
                .DistinctBy(x => (x.User1.ToLowerInvariant(), x.User2.ToLowerInvariant()))
                .OrderBy(x => x.User1, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.User2, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<List<IpLogEntry>> GetKnownIpsForUser(string username, int? limit = null, int? offset = null)
        {
            var query = _context.IpLogEntrys
                .Where(x => x.Username == username);

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
