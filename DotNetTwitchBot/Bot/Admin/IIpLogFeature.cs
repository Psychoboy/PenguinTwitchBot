using DotNetTwitchBot.Bot.Models.IpLogs;
using DotNetTwitchBot.Models;

namespace DotNetTwitchBot.Bot.Admin
{
    public interface IIpLogFeature
    {
        Task<List<IpLogsForUser>> GetDuplicateIpsForUser(string username);
        Task<List<IpLogsForUser>> GetIpLogsForUser(string username);
        Task<PagedDataResponse<IpLogUsersWithSameIp>> GetAllDuplicateIps(int offset, int limit);
    }
}