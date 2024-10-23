using DotNetTwitchBot.Bot.Models.IpLogs;

namespace DotNetTwitchBot.Repository
{
    public interface IIpLogRepository : IGenericRepository<IpLogEntry>
    {
        Task<List<IpLogsForUser>> GetKnownIpsForUser(string username, int? limit = null, int? offset = null);
        Task<List<IpLogsForUser>> GetDuplicateIpsForUser(string username, int? limit = null, int? offset = null);
        IQueryable<IpLogUsersWithSameIp> GetAllUsersWithDuplicateIps(int? limit = null, int? offset = null);
    }
}
