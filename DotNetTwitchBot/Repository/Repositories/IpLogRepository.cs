﻿

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
                        where p1.Username.Equals(p2.Username) == false
                        select new IpLogUsersWithSameIp
                        {
                            User1 = p1.Username,
                            User2 = p2.Username,
                            Ip = p1.Ip
                        };

            query = query.GroupBy(g => new
            {
                g.User1,
                g.User2,
                g.Ip
            }).Select(x => new IpLogUsersWithSameIp
            {
                User1 = x.Key.User1,
                User2 = x.Key.User2,
                Ip = x.Key.Ip,
                Count = x.Count()
            }).OrderBy(x => x.User1);

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
