using PenguinTwitchBot.Database.Bot.Models.IpLogs;
using PenguinTwitchBot.Models;

namespace PenguinTwitchBot.Bot.Admin
{
    public interface IIpLogFeature
    {
        Task<List<IpLogEntry>> GetDuplicateIpsForUser(string username);
        Task<List<IpLogEntry>> GetIpLogsForUser(string username);
        //Task<PagedDataResponse<IpLogUsersWithSameIp>> GetAllDuplicateIps(int offset, int limit);
        Task<PagedDataResponse<IpLogUsersWithSameIp>> GetAllDuplicateIps();
    }
}