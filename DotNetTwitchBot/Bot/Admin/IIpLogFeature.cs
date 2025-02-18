using DotNetTwitchBot.Bot.Models.IpLogs;
using DotNetTwitchBot.Models;

namespace DotNetTwitchBot.Bot.Admin
{
    public interface IIpLogFeature
    {
        Task<List<IpLogEntry>> GetDuplicateIpsForUser(string username);
        Task<List<IpLogEntry>> GetIpLogsForUser(string username);
        Task<PagedDataResponse<IpLogUsersWithSameIp>> GetAllDuplicateIps(int offset, int limit);
    }
}