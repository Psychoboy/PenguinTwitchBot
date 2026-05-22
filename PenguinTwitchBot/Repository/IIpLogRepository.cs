using PenguinTwitchBot.Bot.Models.IpLogs;

namespace PenguinTwitchBot.Repository
{
    public interface IIpLogRepository : IGenericRepository<IpLogEntry>
    {
        Task<List<IpLogEntry>> GetKnownIpsForUser(string username, int? limit = null, int? offset = null);
        Task<List<IpLogEntry>> GetDuplicateIpsForUser(string username, int? limit = null, int? offset = null);
        Task<List<IpLogUsersWithSameIp>> GetAllUsersWithDuplicateIps();
    }
}
