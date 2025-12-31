

using DotNetTwitchBot.Bot.Models.IpLogs;
using System.Diagnostics.CodeAnalysis;

namespace DotNetTwitchBot.Repository.Repositories
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

        public IQueryable<IpLogUsersWithSameIp> GetAllUsersWithDuplicateIps(int? limit = null, int? offset = null)
        {
            var query = from p1 in _context.IpLogEntrys
                        join p2 in _context.IpLogEntrys on p1.Ip equals p2.Ip
                        where p1.Username.CompareTo(p2.Username) < 0
                        select new IpLogUsersWithSameIp
                        {
                            User1 = p1.Username,
                            User2 = p2.Username
                        };

            query = query.Distinct();
            query = query.OrderBy(x => x.User1).ThenBy(x => x.User2);


            if (offset != null)
            {
                query = query.Skip((int)offset);
            }

            if (limit != null)
            {
                query = query.Take((int)limit);
            }

            return query;
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
