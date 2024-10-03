using DotNetTwitchBot.Bot.Models.IpLogs;

namespace DotNetTwitchBot.Bot.Admin
{
    public interface IIpLogFeature
    {
        Task<List<IpLogsForUser>> GetDuplicateIpsForUser(string username);
        Task<List<IpLogsForUser>> GetIpLogsForUser(string username);
        Task<List<IpLogUsersWithSameIp>> GetAllDuplicateIps();
    }
}