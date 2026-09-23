using PenguinTwitchBot.Database.Bot.Models.IpLogs;
using PenguinTwitchBot.Models;

namespace PenguinTwitchBot.Bot.Admin
{
    public interface IIpLogFeature
    {
        Task<List<IpLogEntry>> GetDuplicateIpsForUser(string username, string? userId = null);
        Task<List<IpLogEntry>> GetIpLogsForUser(string username, string? userId = null);
        Task<PagedDataResponse<IpLogUsersWithSameIp>> GetAllDuplicateIps();
    }
}