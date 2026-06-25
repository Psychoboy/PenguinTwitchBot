using PenguinTwitchBot.Database.Bot.Models.IpLogs;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository
{
    public interface IIpLogRepository : IGenericRepository<IpLogEntry>
    {
        Task<List<IpLogEntry>> GetKnownIpsForUser(string username, int? limit = null, int? offset = null);
        Task<List<IpLogEntry>> GetDuplicateIpsForUser(string username, int? limit = null, int? offset = null);
        Task<List<IpLogUsersWithSameIp>> GetAllUsersWithDuplicateIps();
    }
}
